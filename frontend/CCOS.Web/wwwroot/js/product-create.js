

(function () {
    var dropZones = document.querySelectorAll("[data-image-drop]");

    if (dropZones.length === 0) {
        return;
    }

    function isImageFile(file) {
        return file &&
            typeof file.type === "string" &&
            file.type.toLowerCase().indexOf("image/") === 0;
    }

    function showFile(dropZone, file) {
        var input = dropZone.querySelector("[data-image-input]");
        var preview = dropZone.querySelector("[data-image-preview]");
        var placeholder = dropZone.querySelector("[data-image-placeholder]");
        var status = dropZone.querySelector("[data-image-status]");

        if (!input || !preview || !placeholder || !status || !file) {
            return;
        }

        if (isImageFile(file) === false) {
            status.textContent = "That file isn't an image. Choose an image file.";
            input.setAttribute("aria-invalid", "true");

            if (!status.id) {
                status.id = "product-image-status-" + Array.prototype.indexOf.call(dropZones, dropZone);
            }

            input.setAttribute("aria-describedby", status.id);

            // Clear the rejected choice so selecting it again can fire a change event.
            input.value = "";
            return;
        }

        input.removeAttribute("aria-invalid");

        if (!status.id) {
            status.id = "product-image-status-" + Array.prototype.indexOf.call(dropZones, dropZone);
        }

        input.setAttribute("aria-describedby", status.id);

        // Release the previous preview URL when replacing an image.
        if (dropZone.dataset.previewUrl) {
            URL.revokeObjectURL(dropZone.dataset.previewUrl);
        }

        var previewUrl = URL.createObjectURL(file);
        dropZone.dataset.previewUrl = previewUrl;

        preview.src = previewUrl;
        preview.hidden = false;
        placeholder.hidden = true;
        status.textContent = "Selected " + file.name + ". Click or drop to replace.";

        // These files are for local preview only, not form submission.
        // Clearing also lets the user choose the same file again.
        input.value = "";
    }

    dropZones.forEach(function (dropZone) {
        var input = dropZone.querySelector("[data-image-input]");

        if (!input) {
            return;
        }

        input.addEventListener("change", function () {
            if (input.files && input.files.length > 0) {
                showFile(dropZone, input.files[0]);
            }
        });

        dropZone.addEventListener("dragover", function (event) {
            // The browser's default is to open the dropped file.
            // Preventing it allows this element to handle the drop instead.
            event.preventDefault();
            event.dataTransfer.dropEffect = "copy";
            dropZone.classList.add("is-dragging");
        });

        dropZone.addEventListener("dragleave", function (event) {
            // Ignore movement between children inside the drop zone.
            if (event.relatedTarget && dropZone.contains(event.relatedTarget)) {
                return;
            }

            dropZone.classList.remove("is-dragging");
        });

        dropZone.addEventListener("drop", function (event) {
            event.preventDefault();
            dropZone.classList.remove("is-dragging");

            var files = event.dataTransfer && event.dataTransfer.files;
            if (files && files.length > 0) {
                showFile(dropZone, files[0]);
            }
        });
    });

    window.addEventListener("pagehide", function () {
        dropZones.forEach(function (dropZone) {
            if (dropZone.dataset.previewUrl) {
                URL.revokeObjectURL(dropZone.dataset.previewUrl);
            }
        });
    });
})();

// Keeps the Save button clickable after a field shows a validation message.
// Pressing the mouse normally moves focus off the field first. The validator then
// clears that field's message, the form gets shorter, and Save slides out from under
// the cursor before the click finishes, so nothing happens. Keeping focus where it is
// until the click lands means the submit runs and checks every field at once.
(function () {
    var saveButtons = document.querySelectorAll(".product-form-save");

    saveButtons.forEach(function (button) {
        button.addEventListener("mousedown", function (event) {
            // Left button only, so right-click menus still work.
            if (event.button === 0) {
                event.preventDefault();
            }
        });
    });
})();
