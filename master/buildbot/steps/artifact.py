from buildbot.process.buildstep import LoggingBuildStep, SUCCESS, FAILURE, EXCEPTION, SKIPPED
from twisted.internet import defer
from buildbot.steps.shell import ShellCommand
import re
from buildbot.util import epoch2datetime
from buildbot.util import safeTranslate
from buildbot.process.slavebuilder import IDLE, BUILDING

def FormatDatetime(value):
    return value.strftime("%d_%m_%Y_%H_%M_%S_%z")

def mkdt(epoch):
    if epoch:
        return epoch2datetime(epoch)

@defer.inlineCallbacks
def updateSourceStamps(master, build, build_sourcestamps):
    # every build will generate at least one sourcestamp
    sourcestamps = build.build_status.getSourceStamps()

    build_sourcestampsetid = sourcestamps[0].sourcestampsetid

    sourcestamps_updated = build.build_status.getAllGotRevisions()
    build.build_status.updateSourceStamps()

    if len(sourcestamps_updated) > 0:
        update_ss = []
        for key, value in sourcestamps_updated.iteritems():
            update_ss.append(
                {'b_codebase': key, 'b_revision': value, 'b_sourcestampsetid': build_sourcestampsetid})

        rowsupdated = yield master.db.sourcestamps.updateSourceStamps(update_ss)

    # when running rebuild or passing revision as parameter
    for ss in sourcestamps:
        build_sourcestamps.append(
            {'b_codebase': ss.codebase, 'b_revision': ss.revision, 'b_branch': ss.branch,'b_sourcestampsetid': ss.sourcestampsetid})

class FindPreviousSuccessfulBuild(LoggingBuildStep):
    name = "Find Previous Successful Build"
    description="Searching for a previous successful build at the appropriate revision(s)..."
    descriptionDone="Searching complete."

    def __init__(self, **kwargs):
        self.build_sourcestamps = []
        self.master = None
        LoggingBuildStep.__init__(self, **kwargs)

    @defer.inlineCallbacks
    def start(self):
        if self.master is None:
            self.master = self.build.builder.botmaster.parent

        yield updateSourceStamps(self.master, self.build, self.build_sourcestamps)

        clean_build = self.build.getProperty("clean_build", False)
        if type(clean_build) != bool:
            clean_build = (clean_build.lower() == "true")

        if clean_build:
            self.step_status.setText(["Skipping previous build check (making a clean build)."])
            self.finished(SKIPPED)
            return

        prevBuildRequest = yield self.master.db.buildrequests\
            .getBuildRequestBySourcestamps(buildername=self.build.builder.config.name,
                                           sourcestamps=self.build_sourcestamps)

        if prevBuildRequest:
            build_list = yield self.master.db.builds.getBuildsForRequest(prevBuildRequest['brid'])
            # there can be many builds per buildrequest for example (retry) when slave lost connection
            # in this case we will display all the builds related to this build request
            for build in build_list:
                build_num = build['number']
                url = yield self.master.status.getURLForBuildRequest(prevBuildRequest['brid'],
                                                                     self.build.builder.config.name, build_num)
                self.addURL(url['text'], url['path'])
            brid = self.build.requests[0].id
            # we are not building but reusing a previous build
            reuse = yield self.master.db.buildrequests.reusePreviousBuild(brid, prevBuildRequest['brid'])
            self.step_status.stepFinished(SUCCESS)
            self.build.result = SUCCESS
            self.build.allStepsDone()
            return

        self.step_status.setText(["Running build (previous sucessful build not found)."])
        self.finished(SUCCESS)
        return

class CheckArtifactExists(ShellCommand):
    name = "Check if Artifact Exists"
    description="Checking if artifacts exist from a previous build at the appropriate revision(s)..."
    descriptionDone="Searching complete."

    def __init__(self, artifact=None, artifactDirectory=None, artifactServer=None, artifactServerDir=None, artifactServerURL=None, stopBuild=True,**kwargs):
        self.master = None
        self.build_sourcestamps = []
        if not isinstance(artifact, list):
            artifact = [artifact]
        self.artifact = artifact
        self.artifactDirectory = artifactDirectory
        self.artifactServer = artifactServer
        self.artifactServerDir = artifactServerDir
        self.artifactServerURL = artifactServerURL
        self.artifactBuildrequest = None
        self.artifactPath = None
        self.artifactURL = None
        self.stopBuild = stopBuild
        ShellCommand.__init__(self, **kwargs)

    @defer.inlineCallbacks
    def createSummary(self, log):
        artifactlist = list(self.artifact)
        stdio = self.getLog('stdio').readlines()
        notfoundregex = re.compile(r'Not found!!')
        for l in stdio:
            m = notfoundregex.search(l)
            if m:
                break
            if len(artifactlist) == 0:
                break
            for a in artifactlist:
                artifact = a
                if artifact.endswith("/"):
                    artifact = artifact[:-1]
                foundregex = re.compile(r'(%s)' % artifact)
                m = foundregex.search(l)
                if (m):
                    artifactURL = self.artifactServerURL + "/" + self.artifactPath + "/" + a
                    self.addURL(a, artifactURL)
                    artifactlist.remove(a)

        if len(artifactlist) == 0:
            artifactsfound = self.build.getProperty("artifactsfound", True)

            if not artifactsfound:
                return
            else:
                self.build.setProperty("artifactsfound", True, "CheckArtifactExists %s" % self.artifact)

            if self.stopBuild:
                # update buildrequest (artifactbrid) with self.artifactBuildrequest
                brid = self.build.requests[0].id
                reuse = yield self.master.db.buildrequests.reusePreviousBuild(brid, self.artifactBuildrequest['brid'])
                self.step_status.stepFinished(SUCCESS)
                self.build.result = SUCCESS
                self.build.allStepsDone()
        else:
            self.build.setProperty("artifactsfound", False, "CheckArtifactExists %s" % self.artifact)
            self.descriptionDone = ["Artifact not found on server %s." % self.artifactServerURL]

    @defer.inlineCallbacks
    def start(self):
        if self.master is None:
            self.master = self.build.builder.botmaster.parent

        yield updateSourceStamps(self.master, self.build, self.build_sourcestamps)

        clean_build = self.build.getProperty("clean_build", False)
        if type(clean_build) != bool:
            clean_build = (clean_build.lower() == "true")

        if clean_build:
            self.step_status.setText(["Skipping artifact check (making a clean build)."])
            self.finished(SKIPPED)
            return

        self.artifactBuildrequest = yield self.master.db.buildrequests.getBuildRequestBySourcestamps(buildername=self.build.builder.config.name, sourcestamps=self.build_sourcestamps)

        if self.artifactBuildrequest:
            self.step_status.setText(["Artifact has been already generated."])
            self.artifactPath = "%s_%s_%s" % (self.build.builder.config.builddir,
                                              self.artifactBuildrequest['brid'], FormatDatetime(self.artifactBuildrequest['submitted_at']))

            if self.artifactDirectory:
                self.artifactPath += "/%s" %  self.artifactDirectory

            search_artifact = ""
            for a in self.artifact:
                if a.endswith("/"):
                    a = a[:-1]
                    if "/" in a:
                        index = a.rfind("/")
                        a = a[:index] + "/*"
                search_artifact += "; ls %s" % a

            command = ["ssh", self.artifactServer, "cd %s;" % self.artifactServerDir,
                       "if [ -d %s ]; then echo 'Exists'; else echo 'Not found!!'; fi;" % self.artifactPath,
                       "cd %s" % self.artifactPath, search_artifact, "; ls"]
            # ssh to the server to check if it artifact is there
            self.setCommand(command)
            ShellCommand.start(self)
            return


        self.step_status.setText(["Artifact not found."])
        self.finished(SUCCESS)
        return


class CreateArtifactDirectory(ShellCommand):

    name = "Create Remote Artifact Directory"
    description="Creating the artifact directory on the remote artifacts server..."
    descriptionDone="Remote artifact directory created."

    def __init__(self,  artifactDirectory=None, artifactServer=None, artifactServerDir=None,  **kwargs):
        self.artifactDirectory = artifactDirectory
        self.artifactServer = artifactServer
        self.artifactServerDir = artifactServerDir
        ShellCommand.__init__(self, **kwargs)

    def start(self):
        br = self.build.requests[0]
        artifactPath  = "%s_%s_%s" % (self.build.builder.config.builddir,
                                      br.id, FormatDatetime(mkdt(br.submittedAt)))
        if (self.artifactDirectory):
            artifactPath += "/%s" % self.artifactDirectory


        command = ["ssh", self.artifactServer, "cd %s;" % self.artifactServerDir, "mkdir -p ",
                    artifactPath]

        self.setCommand(command)
        ShellCommand.start(self)

class UploadArtifact(ShellCommand):

    name = "Upload Artifact(s)"
    description="Uploading artifact(s) to remote artifact server..."
    descriptionDone="Artifact(s) uploaded."

    def __init__(self, artifact=None, artifactDirectory=None, artifactServer=None, artifactServerDir=None, artifactServerURL=None, **kwargs):
        self.artifact=artifact
        self.artifactURL = None
        self.artifactDirectory = artifactDirectory
        self.artifactServer = artifactServer
        self.artifactServerDir = artifactServerDir
        self.artifactServerURL = artifactServerURL
        ShellCommand.__init__(self, **kwargs)

    @defer.inlineCallbacks
    def start(self):
        br = self.build.requests[0]

        # this means that we are merging build requests with this one
        if len(self.build.requests) > 1:
            master = self.build.builder.botmaster.parent
            reuse = yield master.db.buildrequests.updateMergedBuildRequest(self.build.requests)

        artifactPath  = "%s_%s_%s" % (self.build.builder.config.builddir,
                                      br.id, FormatDatetime(mkdt(br.submittedAt)))
        if (self.artifactDirectory):
            artifactPath += "/%s" % self.artifactDirectory


        remotelocation = self.artifactServer + ":" +self.artifactServerDir + "/" + artifactPath + "/" + self.artifact
        command = ["rsync", "-vazr", self.artifact, remotelocation]

        self.artifactURL = self.artifactServerURL + "/" + artifactPath + "/" + self.artifact
        self.addURL(self.artifact, self.artifactURL)
        self.setCommand(command)
        ShellCommand.start(self)

class DownloadArtifact(ShellCommand):
    name = "Download Artifact(s)"
    description="Downloading artifact(s) from the remote artifacts server..."
    descriptionDone="Artifact(s) downloaded."

    def __init__(self, artifactBuilderName=None, artifact=None, artifactDirectory=None, artifactDestination=None, artifactServer=None, artifactServerDir=None, **kwargs):
        self.artifactBuilderName = artifactBuilderName
        self.artifact = artifact
        self.artifactDirectory = artifactDirectory
        self.artifactServer = artifactServer
        self.artifactServerDir = artifactServerDir
        self.artifactDestination = artifactDestination or artifact
        self.master = None
        name = "Download Artifact for '%s'" % artifactBuilderName
        description = "Downloading artifact '%s'..." % artifactBuilderName
        descriptionDone="Downloaded '%s'." % artifactBuilderName
        ShellCommand.__init__(self, name=name, description=description, descriptionDone=descriptionDone,  **kwargs)

    @defer.inlineCallbacks
    def start(self):
        if self.master is None:
            self.master = self.build.builder.botmaster.parent

        #find artifact dependency
        triggeredbybrid = self.build.requests[0].id
        br = yield self.master.db.buildrequests.getBuildRequestTriggered(triggeredbybrid, self.artifactBuilderName)

        artifactPath  = "%s_%s_%s" % (safeTranslate(self.artifactBuilderName),
                                      br['brid'], FormatDatetime(br["submitted_at"]))
        if (self.artifactDirectory):
            artifactPath += "/%s" % self.artifactDirectory

        remotelocation = self.artifactServer + ":" +self.artifactServerDir + "/" + artifactPath + "/" + self.artifact
        command = ["rsync", "-vazr", remotelocation, self.artifactDestination]
        self.setCommand(command)
        ShellCommand.start(self)

from buildbot import locks

class AcquireBuildLocks(LoggingBuildStep):
    name = "Acquire Builder Locks"
    description="Acquiring builder locks..."
    descriptionDone="Builder locks acquired."

    def __init__(self, hideStepIf = True, **kwargs):
        LoggingBuildStep.__init__(self, hideStepIf = hideStepIf, **kwargs)

    def start(self):
        self.step_status.setText(["Acquiring lock to complete build."])
        self.build.locks = self.locks
        # Acquire lock
        if self.build.slavebuilder.state == IDLE:
            self.build.slavebuilder.state = BUILDING
        if self.build.builder.builder_status.currentBigState == "idle":
            self.build.builder.builder_status.setBigState("building")
        self.build.releaseLockInstanse = self
        self.finished(SUCCESS)
        return

    def releaseLocks(self):
        return

class ReleaseBuildLocks(LoggingBuildStep):
    name = "Release Builder Locks"
    description="Releasing builder locks..."
    descriptionDone="Build locks released."

    def __init__(self, hideStepIf = True, **kwargs):
        self.releaseLockInstanse
        LoggingBuildStep.__init__(self, hideStepIf=hideStepIf, **kwargs)

    def start(self):
        self.step_status.setText(["Releasing build locks."])
        self.locks = self.build.locks
        self.releaseLockInstanse = self.build.releaseLockInstanse
        # release slave lock
        self.build.slavebuilder.state = IDLE
        self.build.builder.builder_status.setBigState("idle")
        self.finished(SUCCESS)
        return
