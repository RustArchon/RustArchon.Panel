// Copyright ©2026 Scott Blomfield
//
// Hands a file to the browser as a download, BYTE FOR BYTE. Imported on demand (an ES module, like reportExport.js).
//
// Deliberately not reportExport.js: that one prepends a byte-order mark and treats its input as text. The RustArchon
// plugin is served signed, and its signature covers its exact bytes, so a single added byte or converted line ending
// would make the file read as tampered on the game server. The bytes arrive base64 encoded and are written out
// unchanged.

export function downloadBytes(filename, base64, contentType) {
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
    }

    const blob = new Blob([bytes], { type: contentType || "application/octet-stream" });
    const url = URL.createObjectURL(blob);

    const link = document.createElement("a");
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    // Deferred: revoking synchronously after click() can cancel the download in some browsers.
    setTimeout(() => URL.revokeObjectURL(url), 0);
}
