
import os

from twisted.application import service
from bot import BuildRequestDistributor

basedir = r'.'
rotateLength = 10000000
maxRotatedFiles = 10

# if this is a relocatable tac file, get the directory containing the TAC
if basedir == '.':
    import os.path
    basedir = os.path.abspath(os.path.dirname(__file__))

# note: this line is matched against to check that this is a service
# directory; do not edit it.
application = service.Application('service')

try:

  from twisted.python.logfile import LogFile
  from twisted.python.log import ILogObserver, FileLogObserver

  logfile = LogFile.fromFullPath(
      os.path.join(basedir, "twistd.log"),
      rotateLength=rotateLength,
      maxRotatedFiles=maxRotatedFiles
  )

  application.setComponent(ILogObserver, FileLogObserver(logfile).emit)

except ImportError:
  # probably not yet twisted 8.2.0 and beyond, can't set log yet
  pass

buildmaster_host = 'localhost'
port = 9901
name = 'build-slave-1'
password = 'builduser'
keepalive = 600
usepty = 0
umask = None
maxdelay = 300

s = BuildRequestDistributor(
    buildmaster_host, port,
    name, password,
    basedir, keepalive,
    usepty, umask=umask,
    maxdelay=maxdelay, allow_shutdown=None
)

s.setServiceParent(application)

