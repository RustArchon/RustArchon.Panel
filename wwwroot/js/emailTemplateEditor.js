// Copyright ©2026 Scott Blomfield
//
// Thin Quill.js wrapper for Admin/EmailTemplates.razor's rich-text body editor - same "plain global,
// not a collocated module" pattern as statsChart.js/consoleScroll.js, since nothing here needs
// per-component isolation either.
//
// Unlike statsChart's canvases, this needs to call back INTO .NET as the admin types, to drive the
// live preview pane - the one thing nothing else in this app has needed a JS-to-.NET push for. Text
// changes are debounced into one call per pause in typing rather than one round-trip per keystroke.

window.emailTemplateEditor = {
    _instances: {},

    // dotNetRef: a DotNetObjectReference<EmailTemplates> with an [JSInvokable] OnBodyChanged(html).
    create: function (editorId, initialHtml, dotNetRef) {
        var container = document.getElementById(editorId);
        if (!container || typeof Quill === "undefined") {
            return;
        }

        window.emailTemplateEditor.destroy(editorId);

        var quill = new Quill(container, {
            theme: "snow",
            modules: {
                toolbar: [
                    [{ header: [false, 2, 3] }],
                    ["bold", "italic", "underline", "link"],
                    [{ list: "ordered" }, { list: "bullet" }],
                    ["clean"]
                ]
            }
        });
        quill.root.innerHTML = initialHtml || "";

        var debounceHandle = null;
        quill.on("text-change", function () {
            if (debounceHandle) {
                clearTimeout(debounceHandle);
            }
            debounceHandle = setTimeout(function () {
                dotNetRef.invokeMethodAsync("OnBodyChanged", quill.root.innerHTML);
            }, 400);
        });

        window.emailTemplateEditor._instances[editorId] = quill;
    },

    getHtml: function (editorId) {
        var quill = window.emailTemplateEditor._instances[editorId];
        return quill ? quill.root.innerHTML : "";
    },

    // Inserts text at the editor's current cursor (focusing it first if it wasn't already, so a chip
    // clicked right after opening the editor still lands somewhere sensible rather than doing nothing)
    // - used by the "insert placeholder" chips.
    insertToken: function (editorId, token) {
        var quill = window.emailTemplateEditor._instances[editorId];
        if (!quill) {
            return;
        }
        var range = quill.getSelection(true) || { index: quill.getLength(), length: 0 };
        quill.insertText(range.index, token, "user");
        quill.setSelection(range.index + token.length, 0);
    },

    // Same idea as insertToken, for the plain Subject <input> - splices at the real caret position
    // and dispatches a native "input" event so the InputText it's bound to (@bind-Value:event="oninput")
    // picks up the change exactly as if the admin had typed it.
    insertIntoInput: function (inputId, token) {
        var input = document.getElementById(inputId);
        if (!input) {
            return;
        }
        var start = input.selectionStart ?? input.value.length;
        var end = input.selectionEnd ?? input.value.length;
        input.value = input.value.slice(0, start) + token + input.value.slice(end);
        var caret = start + token.length;
        input.focus();
        input.setSelectionRange(caret, caret);
        input.dispatchEvent(new Event("input", { bubbles: true }));
    },

    destroy: function (editorId) {
        delete window.emailTemplateEditor._instances[editorId];
    }
};
