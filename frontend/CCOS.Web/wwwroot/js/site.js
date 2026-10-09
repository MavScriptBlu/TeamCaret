// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

(function () {
  var toggle = document.getElementById('menuToggle');
  var menu = document.getElementById('siteMenu');
  var scrim = document.getElementById('menuScrim');
  var closeBtn = document.getElementById('menuClose');
  if (!toggle || !menu || !scrim || !closeBtn) return;

  function setOpen(open) {
    menu.hidden = !open;
    scrim.hidden = !open;
    toggle.setAttribute('aria-expanded', String(open));
    (open ? closeBtn : toggle).focus();
  }

  toggle.addEventListener('click', function () { setOpen(true); });
  closeBtn.addEventListener('click', function () { setOpen(false); });
  scrim.addEventListener('click', function () { setOpen(false); });
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && !menu.hidden) setOpen(false);
  });
})();