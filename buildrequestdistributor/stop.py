import os
import time
import base
from twisted.python.runtime import platformType


class ServiceNotRunning(Exception):
    """
    raised when trying to stop the service process that is not running
    """


def stopService(basedir, quiet, signame="TERM"):
    """
    Stop the service process by sending it a signal.

    Using the specified basedir path, read the service process's pid file and
    try to terminate that process with specified signal.

    @param basedir: service's basedir path
    @param   quite: if False, don't print any messages to stdout
    @param signame: signal to send to the the service process

    @raise ServiceNotRunning: if the service pid file is not found
    """
    import signal

    pidfile = os.path.join(basedir, 'twistd.pid')
    try:
        with open(pidfile, "rt") as f:
            pid = int(f.read().strip())
    except:
        raise ServiceNotRunning()

    signum = getattr(signal, "SIG" + signame)
    timer = 0
    try:
        if base.isServiceRunning(basedir, quiet):
            os.kill(pid, signum)
            if platformType == "win32" and os.path.exists(pidfile):
                os.unlink(pidfile)

    except OSError, e:
        if e.errno != 3:
            raise

    time.sleep(0.1)
    while timer < 10:
        # poll once per second until twistd.pid goes away, up to 10 seconds
        try:
            os.kill(pid, 0)
        except OSError:
            if not quiet:
                print "service process %d is dead" % pid
            return
        timer += 1
        time.sleep(1)
    if not quiet:
        print "never saw process go away"


def stop(config, signame="TERM"):
    quiet = config['quiet']
    basedir = config['basedir']

    if not base.isServiceDir(basedir):
        return 1

    try:
        stopService(basedir, quiet, signame)
    except ServiceNotRunning:
        if not quiet:
            print "service not running"

    return 0
