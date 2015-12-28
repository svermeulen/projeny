
from upm.ioc.Inject import Inject
from upm.ioc.Inject import InjectMany
import upm.ioc.IocAssertions as Assertions

from upm.reg.ReleaseInfo import ReleaseInfo, createReleaseInfoFromPath

import os

from upm.util.Assert import *

class FileInfo:
    def __init__(self, path, release):
        self.path = path
        self.release = release

class LocalFolderRegistry:
    _log = Inject('Logger')
    _sys = Inject('SystemHelper')
    _varMgr = Inject('VarManager')
    _extractor = Inject('UnityPackageExtractor')

    def __init__(self, settings):
        self._folderPath = self._varMgr.expand(settings['Path']).replace("\\", "/")
        self._files = []

    @property
    def releases(self):
        return [x.release for x in self._files]

    def init(self):
        self._log.heading('Initializing registry for local folder "{0}"', self._folderPath)

        for path in self._sys.findFilesByPattern(self._folderPath, '*.unitypackage'):
            release = createReleaseInfoFromPath(path)

            self._files.append(FileInfo(path, release))

        self._log.info("Found {0} released in folder '{1}'", len(self._files), self._folderPath)

    def getName(self):
        return "Local Folder ({0})".format(self._folderPath)

    def installRelease(self, releaseInfo, outputDir):
        fileInfo = next(x for x in self._files if x.release == releaseInfo)
        assertIsNotNone(fileInfo)

        self._extractor.extractUnityPackage(fileInfo.path, outputDir)

