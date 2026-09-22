// Copyright ©2026 Scott Blomfield
//
// Quill.js wrapper for the plan announcement page - separate from emailTemplateEditor.js on purpose: that one edits the HTML of a template, this one edits
// a message that is sent as Quill's own structured content (the "Delta"), never as HTML. The server builds the email's HTML from the Delta and nothing
// else, so nothing an editor produces can carry markup into an email.
//
// One editor per language. Changes are debounced into one call back into .NET per pause in typing.

window.announcementEditor = {
    _instances: {},

    // dotNetRef: a DotNetObjectReference with an [JSInvokable] OnBodyChanged(culture, deltaJson, hasText).
    create: function (editorId, initialDeltaJson, dotNetRef, culture) {
        var container = document.getElementById(editorId);
        if (!container || typeof Quill === "undefined") {
            return;
        }

        window.announcementEditor.destroy(editorId);

        var quill = new Quill(container, {
            theme: "snow",
            // Only what the server will keep: anything else pasted in is dropped by the editor before it is ever sent.
            formats: ["header", "bold", "italic", "underline", "strike", "link", "list", "blockquote"],
            modules: {
                toolbar: [
                    [{ header: [false, 1, 2, 3] }],
                    ["bold", "italic", "underline", "strike", "link"],
                    [{ list: "ordered" }, { list: "bullet" }],
                    ["blockquote", "clean"]
                ]
            }
        });

        if (initialDeltaJson) {
            try {
                quill.setContents(JSON.parse(initialDeltaJson));
            } catch (e) {
                // A message that will not load is an empty one: better than an editor that will not open.
            }
        }

        var debounceHandle = null;
        quill.on("text-change", function () {
            if (debounceHandle) {
                clearTimeout(debounceHandle);
            }
            debounceHandle = setTimeout(function () {
                dotNetRef.invokeMethodAsync(
                    "OnBodyChanged", culture, JSON.stringify(quill.getContents()), quill.getText().trim().length > 0);
            }, 300);
        });

        window.announcementEditor._instances[editorId] = quill;
    },

    // Inserts text at the editor's cursor (focusing it first) - the "insert a name" chips.
    insertToken: function (editorId, token) {
        var quill = window.announcementEditor._instances[editorId];
        if (!quill) {
            return;
        }
        var range = quill.getSelection(true) || { index: quill.getLength(), length: 0 };
        quill.insertText(range.index, token, "user");
        quill.setSelection(range.index + token.length, 0);
    },

    // The same, for the plain subject input: splices at the caret and dispatches a native "input" event so the bound value follows.
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
        delete window.announcementEditor._instances[editorId];
    }
};
