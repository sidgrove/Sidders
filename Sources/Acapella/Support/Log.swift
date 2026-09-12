import OSLog

enum Log {
    static let audio = Logger(subsystem: "com.sidgrove.acapella", category: "audio")
    static let speech = Logger(subsystem: "com.sidgrove.acapella", category: "speech")
    static let hotkey = Logger(subsystem: "com.sidgrove.acapella", category: "hotkey")
    static let inject = Logger(subsystem: "com.sidgrove.acapella", category: "inject")
    static let app = Logger(subsystem: "com.sidgrove.acapella", category: "app")
}
