// Copyright ©2026 Scott Blomfield
//
// Scroll helpers for ServerDetail.razor's live console/chat panes. Kept as a tiny global (matching
// theme.js's own pattern in this project) rather than a collocated module - there's nothing here
// that needs per-component isolation.

window.consoleScroll = {
    // Called from C# right before a new line is appended to the DOM, so it reflects the pane's
    // scroll position *before* that line's height is added - the only reliable way to answer "was
    // the user already at the bottom" without a live scroll-event listener.
    isNearBottom: function (element, thresholdPx) {
        if (!element) {
            return true;
        }
        var threshold = typeof thresholdPx === "number" ? thresholdPx : 60;
        return element.scrollHeight - element.scrollTop - element.clientHeight <= threshold;
    },

    scrollToBottom: function (element) {
        if (element) {
            element.scrollTop = element.scrollHeight;
        }
    }
};
