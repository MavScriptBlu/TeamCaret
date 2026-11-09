
// This script handles the deletion of officer products in the UI.
// It shows a confirmation dialog when a delete button is clicked,
// and submits the associated form if the user confirms the deletion.
(function () {
  var dialog = document.getElementById('deleteProductDialog');
  var productName = document.getElementById('deleteProductName');
  var confirmButton = document.getElementById('confirmProductDelete');
  var cancelButton = document.getElementById('cancelProductDelete');

  if (!dialog || !productName || !confirmButton || !cancelButton) return;

  var pendingForm = null;
  var opener = null;
  var highlightedRow = null;

// Positions the dialog relative to the button that triggered it, ensuring it stays within the viewport.
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

// Event listener for clicks on delete buttons, showing the confirmation dialog.
  document.addEventListener('click', function (event) {
    var button = event.target.closest('[data-delete-button]');
    if (!button) return;

    var form = button.closest('[data-delete-form]');
    var row = button.closest('.officer-product');
    if (!form || !row) return;

    pendingForm = form;
    opener = button;
    highlightedRow = row;
    productName.textContent = button.dataset.productName || 'this product';
    row.classList.add('is-delete-confirming');

    dialog.showModal();
    positionDialog(button);
    cancelButton.focus();
  });

// Event listener for the confirm button, submitting the form if confirmed.
  confirmButton.addEventListener('click', function () {
    if (pendingForm) {
      pendingForm.requestSubmit();
    }
  });

// Event listener for the cancel button, closing the dialog without submitting.
  cancelButton.addEventListener('click', function () {
    dialog.close('cancel');
  });

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
