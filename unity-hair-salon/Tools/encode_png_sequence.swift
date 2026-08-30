import AppKit
import AVFoundation
import CoreVideo

guard CommandLine.arguments.count == 4 else {
    fputs("usage: encode_png_sequence <frames-dir> <output.mp4> <fps>\n", stderr)
    exit(64)
}

let frameDirectory = URL(fileURLWithPath: CommandLine.arguments[1], isDirectory: true)
let outputURL = URL(fileURLWithPath: CommandLine.arguments[2])
let fps = Int32(CommandLine.arguments[3]) ?? 10
let manager = FileManager.default
let frameURLs = try manager.contentsOfDirectory(
    at: frameDirectory,
    includingPropertiesForKeys: nil
).filter { $0.pathExtension.lowercased() == "png" }
 .sorted { $0.lastPathComponent < $1.lastPathComponent }

guard let firstURL = frameURLs.first,
      let firstImage = NSImage(contentsOf: firstURL),
      let firstCG = firstImage.cgImage(forProposedRect: nil, context: nil, hints: nil) else {
    fputs("no readable PNG frames\n", stderr)
    exit(65)
}

try? manager.removeItem(at: outputURL)
let writer = try AVAssetWriter(outputURL: outputURL, fileType: .mp4)
let settings: [String: Any] = [
    AVVideoCodecKey: AVVideoCodecType.h264,
    AVVideoWidthKey: firstCG.width,
    AVVideoHeightKey: firstCG.height,
    AVVideoCompressionPropertiesKey: [
        AVVideoAverageBitRateKey: 4_000_000,
        AVVideoExpectedSourceFrameRateKey: fps
    ]
]
let input = AVAssetWriterInput(mediaType: .video, outputSettings: settings)
input.expectsMediaDataInRealTime = false
let attributes: [String: Any] = [
    kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32ARGB,
    kCVPixelBufferWidthKey as String: firstCG.width,
    kCVPixelBufferHeightKey as String: firstCG.height
]
let adaptor = AVAssetWriterInputPixelBufferAdaptor(
    assetWriterInput: input,
    sourcePixelBufferAttributes: attributes
)
guard writer.canAdd(input) else {
    fputs("cannot add video input\n", stderr)
    exit(66)
}
writer.add(input)
guard writer.startWriting() else {
    fputs("writer failed to start: \(writer.error?.localizedDescription ?? "unknown")\n", stderr)
    exit(67)
}
writer.startSession(atSourceTime: .zero)

func pixelBuffer(from image: CGImage) -> CVPixelBuffer? {
    var buffer: CVPixelBuffer?
    let result = CVPixelBufferCreate(
        kCFAllocatorDefault,
        image.width,
        image.height,
        kCVPixelFormatType_32ARGB,
        attributes as CFDictionary,
        &buffer
    )
    guard result == kCVReturnSuccess, let buffer else { return nil }
    CVPixelBufferLockBaseAddress(buffer, [])
    defer { CVPixelBufferUnlockBaseAddress(buffer, []) }
    guard let base = CVPixelBufferGetBaseAddress(buffer),
          let context = CGContext(
            data: base,
            width: image.width,
            height: image.height,
            bitsPerComponent: 8,
            bytesPerRow: CVPixelBufferGetBytesPerRow(buffer),
            space: CGColorSpaceCreateDeviceRGB(),
            bitmapInfo: CGImageAlphaInfo.noneSkipFirst.rawValue
          ) else { return nil }
    context.draw(image, in: CGRect(x: 0, y: 0, width: image.width, height: image.height))
    return buffer
}

for (index, url) in frameURLs.enumerated() {
    while !input.isReadyForMoreMediaData { usleep(1_000) }
    guard let image = NSImage(contentsOf: url),
          let cg = image.cgImage(forProposedRect: nil, context: nil, hints: nil),
          let buffer = pixelBuffer(from: cg) else {
        fputs("failed to decode \(url.path)\n", stderr)
        exit(68)
    }
    let time = CMTime(value: CMTimeValue(index), timescale: fps)
    guard adaptor.append(buffer, withPresentationTime: time) else {
        fputs("failed to append frame \(index): \(writer.error?.localizedDescription ?? "unknown")\n", stderr)
        exit(69)
    }
}

input.markAsFinished()
let semaphore = DispatchSemaphore(value: 0)
writer.finishWriting { semaphore.signal() }
semaphore.wait()
guard writer.status == .completed else {
    fputs("video encode failed: \(writer.error?.localizedDescription ?? "unknown")\n", stderr)
    exit(70)
}
print("encoded \(frameURLs.count) frames to \(outputURL.path)")
