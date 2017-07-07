import os, sys, re
from twisted.python import usage, reflect

# the create/start/stop commands should all be run as the same user,
# preferably a separate 'buildbot' account.

# Note that the terms 'options' and 'config' are used interchangeably here - in
# fact, they are interchanged several times.  Caveat legator.


class MakerBase(usage.Options):
    optFlags = [
        ['help', 'h', "Display this message"],
        ["quiet", "q", "Do not emit the commands being run"],
        ]

    longdesc = """
    Operates upon the specified <basedir> (or the current directory, if not
    specified).
    """

    opt_h = usage.Options.opt_help

    def parseArgs(self, *args):
        if len(args) > 0:
            self['basedir'] = args[0]
        else:
            # Use the current directory if no basedir was specified.
            self['basedir'] = os.getcwd()
        if len(args) > 1:
            raise usage.UsageError("I wasn't expecting so many arguments")

    def postOptions(self):
        self['basedir'] = os.path.abspath(self['basedir'])


class StartOptions(MakerBase):
    subcommandFunction = "start.startCommand"
    optFlags = [
        ['quiet', 'q', "Don't display startup log messages"],
        ['nodaemon', None, "Don't daemonize (stay in foreground)"],
        ]
    def getSynopsis(self):
        return "Usage:    python service.py start"


class StopOptions(MakerBase):
    subcommandFunction = "stop.stop"
    def getSynopsis(self):
        return "Usage:    python service.py stop"


class RestartOptions(MakerBase):
    subcommandFunction = "restart.restart"
    optFlags = [
        ['quiet', 'q', "Don't display startup log messages"],
        ['nodaemon', None, "Don't daemonize (stay in foreground)"],
        ]
    def getSynopsis(self):
        return "Usage:     python service.py restart"


class Options(usage.Options):
    synopsis = "Usage:    python service.py <command> [command options]"

    subCommands = [
        # the following are all admin commands
        ['start', None, StartOptions, "Start the service"],
        ['stop', None, StopOptions, "Stop the service"],
        ['restart', None, RestartOptions,
         "Restart the sesrvice"],
        ]

    def postOptions(self):
        if not hasattr(self, 'subOptions'):
            raise usage.UsageError("must specify a command")


def run():
    config = Options()
    try:
        config.parseOptions()
    except usage.error, e:
        print "%s:  %s" % (sys.argv[0], e)
        print
        c = getattr(config, 'subOptions', config)
        print str(c)
        sys.exit(1)

    subconfig = config.subOptions
    subcommandFunction = reflect.namedObject(subconfig.subcommandFunction)
    sys.exit(subcommandFunction(subconfig))
