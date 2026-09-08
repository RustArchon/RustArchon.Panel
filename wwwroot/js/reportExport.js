// Copyright ©2026 Scott Blomfield
//
// Hands a generated CSV to the browser as a download. An ES module rather than a global on window
// (which is what consoleScroll.js and statsChart.js are), because it is imported on demand by
// ReportGrid rather than loaded on every page - a report export is rare, and this way App.razor
// doesn't need a script tag for it.

// Written as an escape rather than the character itself so it survives being opened, saved and
// diffed by editors that would otherwise strip or duplicate a leading byte-order mark.
const BOM = "\ufeff";

export function download(filename, text) {
    // The BOM is what makes Excel read the file as UTF-8. Without it, an organization name with an
    // accent in it opens as mojibake on a default Windows install - which is exactly the kind of
    // thing nobody notices until a customer sees their own name spelled wrong.
    const blob = new Blob([BOM + text], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);

    const link = document.createElement("a");
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    // Deferred: revoking synchronously after click() can cancel the download in some browsers before
    // it has read the blob.
    setTimeout(() => URL.revokeObjectURL(url), 0);
}
