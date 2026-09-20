// Copyright ©2026 Scott Blomfield
//
// Puts an F7 report's screenshot into an <img>. The picture is streamed from the Api through the Panel as bytes and shown from a blob:
// address that exists only in this page - it has no address of its own that could be bookmarked, cached by a shared proxy or handed to
// someone who may not read reports.
window.reportScreenshot = (function () {
    var urls = {};

    function forget(imgId) {
        if (urls[imgId]) {
            URL.revokeObjectURL(urls[imgId]);
            delete urls[imgId];
        }
    }

    return {
        show: async function (imgId, contentType, streamRef) {
            var buffer = await streamRef.arrayBuffer();
            var img = document.getElementById(imgId);
            if (!img) {
                return;
            }

            forget(imgId);
            urls[imgId] = URL.createObjectURL(new Blob([buffer], { type: contentType || "image/jpeg" }));
            img.src = urls[imgId];
        },

        clear: function (imgId) {
            forget(imgId);
            var img = document.getElementById(imgId);
            if (img) {
                img.removeAttribute("src");
            }
        }
    };
})();
