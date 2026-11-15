// Handles the "Remove?" confirmation on the officer venue and event pages (CAR-56, CAR-58).
// A Remove button opens the dialog from Pages/Shared/_RemoveDialog.cshtml. "Yes, remove"
// submits the form around the button. "No" closes the dialog and nothing happens.
//
// Markup it looks for:
//   <form data-remove-form> ... <button type="button" data-remove-button data-remove-name="Main Hall"
//                                       data-remove-row=".officer-product">Remove venue</button> </form>
// An optional data-remove-warning on the button shows a warning in the dialog (CAR-79: upcoming
// events booked at the venue). The officer can still remove it.
(function () {
  var dialog = document.getElementById('removeDialog');
  var removeName = document.getElementById('removeName');
  var removeWarning = document.getElementById('removeWarning');
  var confirmButton = document.getElementById('confirmRemove');
  var cancelButton = document.getElementById('cancelRemove');

  if (!dialog || !removeName || !confirmButton || !cancelButton) return;

  var pendingForm = null;
  var opener = null;
  var highlightedRow = null;

  // Puts the dialog next to the button that opened it, kept inside the window.
  function positionDialog(button) {
    var buttonRect = button.getBoundingClientRect();
    var margin = 16;
    var gap = 8;
    var left = buttonRect.left;
    var top = buttonRect.bottom + gap;

    if (left + dialog.offsetWidth > window.innerWidth - margin) {
      left = window.innerWidth - dialog.offsetWidth - margin;
    }
    left = Math.max(margin, left);

    if (top + dialog.offsetHeight > window.innerHeight - margin) {
      top = buttonRect.top - dialog.offsetHeight - gap;
    }
    top = Math.max(margin, Math.min(top, window.innerHeight - dialog.offsetHeight - margin));

    dialog.style.left = left + 'px';
    dialog.style.top = top + 'px';
    dialog.style.transform = 'none';
  }

  // Any click on a Remove button opens the dialog for that item.
  document.addEventListener('click', function (event) {
    var button = event.target.closest('[data-remove-button]');
    if (!button) return;

    var form = button.closest('[data-remove-form]');
    if (!form) return;

    pendingForm = form;
    opener = button;

    // Outline the row being removed, if the button says which one it is.
    var rowSelector = button.dataset.removeRow;
    highlightedRow = rowSelector ? button.closest(rowSelector) : null;
    if (highlightedRow) {
      highlightedRow.classList.add('is-delete-confirming');
    }

    removeName.textContent = button.dataset.removeName || 'this item';

    if (removeWarning) {
      var warning = button.dataset.removeWarning || '';
      removeWarning.textContent = warning;
      removeWarning.hidden = !warning;
    }

    dialog.showModal();
    positionDialog(button);
    cancelButton.focus();
  });

  confirmButton.addEventListener('click', function () {
    if (pendingForm) {
      pendingForm.requestSubmit();
    }
  });

  cancelButton.addEventListener('click', function () {
    dialog.close('cancel');
  });

  // Whatever way the dialog closes (No, Escape, or after submit), put things back.
  dialog.addEventListener('close', function () {
    if (highlightedRow) {
      highlightedRow.classList.remove('is-delete-confirming');
    }

    var focusTarget = opener;
    pendingForm = null;
    opener = null;
    highlightedRow = null;
    dialog.style.left = '';
    dialog.style.top = '';
    dialog.style.transform = '';

    if (focusTarget && focusTarget.isConnected) {
      focusTarget.focus();
    }
  });
})();
