# This file is part of Buildbot.  Buildbot is free software: you can
# redistribute it and/or modify it under the terms of the GNU General Public
# License as published by the Free Software Foundation, version 2.
#
# This program is distributed in the hope that it will be useful, but WITHOUT
# ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
# FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more
# details.
#
# You should have received a copy of the GNU General Public License along with
# this program; if not, write to the Free Software Foundation, Inc., 51
# Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.
#
# Copyright Buildbot Team Members
import json
import re
from twisted.python import log
from os.path import join, basename, splitext
from buildbot.status.web.base import HtmlResource, path_to_builder, path_to_builders, path_to_codebases, path_to_build


class JSONTestResource(HtmlResource):
    def __init__(self, log, step_status):
        HtmlResource.__init__(self)
        self.log = log
        self.step_status = step_status

    def get_artifact_path(self, build):
        server_path = build.getProperty("artifactServerPath", "")
        dir_path = build.getProperty("TestReportUploadDirectory", "")

        return join(server_path, dir_path)

    def content(self, req, cxt):
        s = self.step_status
        b = s.getBuild()

        builder_status = b.getBuilder()
        project = builder_status.getProject()

        cxt['builder_name'] = builder_status.getFriendlyName()
        cxt['path_to_builder'] = path_to_builder(req, builder_status)
        cxt['path_to_builders'] = path_to_builders(req, project)
        cxt['path_to_codebases'] = path_to_codebases(req, project)
        cxt['path_to_build'] = path_to_build(req, b)
        cxt['build_number'] = b.getNumber()
        cxt['path_to_artifacts'] = self.get_artifact_path(b)
        cxt['join'] = join
        cxt['basename'] = basename
        cxt['splitext'] = splitext
        cxt['selectedproject'] = project
        cxt['removeTestFilter'] = removeTestFilter
        cxt['results'] = {
            0: 'Inconclusive',
            1: 'NotRunnable',
            2: 'Skipped',
            3: 'Ignored',
            4: 'Success',
            5: 'Failed',
            6: 'Error',
            7: 'Cancelled'
        }

        json_data = None
        error_message = None

        try:
            json_data = json.loads(self.log.getText())

            if json_data is not None:
                if 'summary' in json_data:
                    success_count = json_data['summary']['successCount']
                    total_count = json_data['summary']['testsCount']
                    if success_count != 0 and total_count != 0:
                        success_per = (float(success_count) / float(total_count)) * 100.0
                        json_data['summary']['success_rate'] = success_per

                json_data['filters'] = {
                    'Inconclusive': True,
                    'Skipped': False,
                    'Ignored': False,
                    'Success': False,
                    'Failed': True,
                    'Error': True,
                    'Cancelled': True
                }

                cxt['data'] = json_data
            else:
                error_message = "[{0}] Error occurred while parsing JSON test report data" \
                    .format(self.__class__.__name__)
        except KeyError as e:
            error_message = "[{0}] Key error in json: {1}".format(self.__class__.__name__, e)
        except:
            import sys
            import traceback
            extype, ex, tb = sys.exc_info()
            formatted = traceback.format_exception_only(extype, ex)[-1]
            error_message = "[{0}] Unexpected exception caught while loading JSON test report data: {1}"\
                .format(self.__class__.__name__, '\n\t'.join(formatted.splitlines()))

        if error_message is not None:
            cxt['data_error'] = error_message
            log.msg(error_message)

        template = req.site.buildbot_service.templates.get_template("jsontestresults.html")
        return template.render(**cxt)

def removeTestFilter(s):
    if (s is None):
        return s
    return re.sub('[-]{1,2}testfilter=.*\s', '', s)
