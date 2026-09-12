import Foundation

/// Where the app keeps its files on disk.
///
/// The folder was `MurmurYouTube` until the module was renamed on 2026-09-12. The first
/// launch of a renamed build moves that folder to `Acapella` — dictionary, run log and
/// dashboard together — so an existing user notices nothing. If both exist (someone ran
/// both builds), the new one wins and the old is left where it is.
enum AppPaths {
    static let folderName = "Acapella"
    private static let legacyFolderName = "MurmurYouTube"

    static var supportDirectory: URL {
        let root = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        let current = root.appendingPathComponent(folderName, isDirectory: true)
        let legacy = root.appendingPathComponent(legacyFolderName, isDirectory: true)

        let files = FileManager.default
        if !files.fileExists(atPath: current.path), files.fileExists(atPath: legacy.path) {
            try? files.moveItem(at: legacy, to: current)
        }
        try? files.createDirectory(at: current, withIntermediateDirectories: true)
        return current
    }
}
