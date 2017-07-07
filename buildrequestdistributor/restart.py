import base, start, stop


def restart(config):
    quiet = config['quiet']
    basedir = config['basedir']

    if not base.isServiceDir(basedir):
        return 1

    try:
        stop.stopService(basedir, quiet)
    except stop.ServiceNotRunning:
        if not quiet:
            print "no old service process found to stop"
    if not quiet:
        print "now restarting service process.."

    return start.startService(basedir, quiet, config['nodaemon'])
