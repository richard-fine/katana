import os
import psutil


def isServiceDir(dir):
    def print_error(error_message):
        print "%s\ninvalid Service directory '%s'" % (error_message, dir)

    buildbot_tac = os.path.join(dir, "buildbot.tac")
    try:
        contents = open(buildbot_tac).read()
    except IOError, exception:
        print_error("error reading '%s': %s" % \
                       (buildbot_tac, exception.strerror))
        return False

    if "Application('service')" not in contents:
        print_error("unexpected content in '%s'" % buildbot_tac)
        return False

    return True


def printMessage(message, quiet):
    if not quiet:
        print message


def isServiceRunning(basedir, quiet):
    pidfile = os.path.join(basedir, 'twistd.pid')

    if os.path.isfile(pidfile):
        try:
            with open(pidfile, "r") as f:
                pid = int(f.read().strip())

            if psutil.pid_exists(pid) and any('service' in argument.lower()
                                              for argument in psutil.Process(pid).cmdline()):
                printMessage(message="service is running", quiet=quiet)
                return True

            printMessage(
                    message="Removing twistd.pid, file has pid {} but service is not running".format(pid),
                    quiet=quiet)
            os.unlink(pidfile)

        except Exception as ex:
            print "An exception has occurred while checking twistd.pid"
            print ex
            raise

    return False