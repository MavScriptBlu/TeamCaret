(function () {
  var dialog = document.getElementById('cancelOrderDialog');
  var orderId = document.getElementById('cancelOrderId');
  var confirmButton = document.getElementById('confirmOrderCancel');
  var dismissButton = document.getElementById('dismissOrderCancel');

  if (!dialog || !orderId || !confirmButton || !dismissButton) return;

  var pendingForm = null;
  var opener = null;
  var highlightedCard = null;

  function positionDialog(button) {
    var bounds = button.getBoundingClientRect();
    var margin = 16;
    var gap = 8;
    var left = Math.max(margin, Math.min(bounds.left, window.innerWidth - dialog.offsetWidth - margin));
    var top = bounds.bottom + gap;

    if (top + dialog.offsetHeight > window.innerHeight - margin) {
      top = bounds.top - dialog.offsetHeight - gap;
    }

    dialog.style.left = left + 'px';
    dialog.style.top = Math.max(margin, Math.min(top, window.innerHeight - dialog.offsetHeight - margin)) + 'px';
    dialog.style.transform = 'none';
  }

  document.addEventListener('click', function (event) {
    var button = event.target.closest('[data-order-cancel-button]');
    if (!button) return;

    var form = button.closest('[data-order-cancel-form]');
    var card = button.closest('.order-card');
    if (!form || !card) return;

    pendingForm = form;
    opener = button;
    highlightedCard = card;
    orderId.textContent = '#' + (button.dataset.orderId || '?');
    card.classList.add('is-delete-confirming');

    dialog.showModal();
    positionDialog(button);
    dismissButton.focus();
  });

  confirmButton.addEventListener('click', function () {
    if (pendingForm) pendingForm.requestSubmit();
  });

  dismissButton.addEventListener('click', function () {
    dialog.close('cancel');
  });

  dialog.addEventListener('close', function () {
    if (highlightedCard) highlightedCard.classList.remove('is-delete-confirming');

    var focusTarget = opener;
    pendingForm = null;
    opener = null;
    highlightedCard = null;
    dialog.style.left = '';
    dialog.style.top = '';
    dialog.style.transform = '';

    if (focusTarget && focusTarget.isConnected) focusTarget.focus();
  });
})();