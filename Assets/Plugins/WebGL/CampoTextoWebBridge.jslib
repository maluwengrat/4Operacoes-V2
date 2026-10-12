mergeInto(LibraryManager.library, {

  CampoTextoWeb_EhMobile: function () {
    var ua = navigator.userAgent || '';
    var mobile = /iPhone|iPad|iPod|Android|webOS|BlackBerry|IEMobile|Opera Mini/i.test(ua)
      || (/Macintosh/i.test(ua) && navigator.maxTouchPoints > 1);
    return mobile ? 1 : 0;
  },

  CampoTextoWeb_Iniciar: function (goNamePtr) {
    if (window.__ctw) return;
    var S = window.__ctw = { goName: UTF8ToString(goNamePtr), el: null, cfg: null };

    S.enviar = function (metodo, valor) {
      try { window.unityInstance.SendMessage(S.goName, metodo, valor); } catch (e) {}
    };

    S.fechar = function (avisar) {
      var el = S.el;
      S.cfg = null;
      if (!el) return;
      S.el = null;
      if (el.parentNode) el.parentNode.removeChild(el);
      if (avisar) S.enviar('OnFecharWeb', '');
    };

    S.abrir = function () {
      var cfg = S.cfg;
      if (!cfg) return;
      var el = S.el;

      if (!el) {
        el = document.createElement('input');
        S.el = el;
        el.setAttribute('autocomplete', 'off');
        el.setAttribute('autocorrect', 'off');
        el.setAttribute('autocapitalize', 'off');
        el.setAttribute('spellcheck', 'false');
        el.style.cssText =
          'position:fixed;top:12px;left:50%;transform:translateX(-50%);' +
          'width:min(92vw,520px);padding:12px 14px;font-size:18px;' +
          'background:#111;color:#fff;border:2px solid #ff6a00;border-radius:10px;' +
          'outline:none;z-index:100000;box-sizing:border-box;';

        el.addEventListener('input', function () { S.enviar('OnTextoWeb', el.value); });
        el.addEventListener('keydown', function (e) {
          if (e.key === 'Enter') { e.preventDefault(); S.fechar(true); }
        });
        el.addEventListener('blur', function () {
          setTimeout(function () { if (S.el === el) S.fechar(true); }, 150);
        });

        // Em tela cheia só aparece o que estiver dentro do elemento em tela cheia
        var host = document.fullscreenElement || document.webkitFullscreenElement || document.body;
        host.appendChild(el);
      }

      el.type = cfg.senha ? 'password' : 'text';
      el.placeholder = cfg.placeholder || '';
      if (cfg.maxLen > 0) el.maxLength = cfg.maxLen; else el.removeAttribute('maxlength');
      if (el.value !== cfg.valor) el.value = cfg.valor;
      el.focus();
    };

    // iOS só mostra o teclado se o foco acontecer dentro do toque do usuário
    var gesto = function () { if (S.cfg) S.abrir(); };
    document.addEventListener('touchend', gesto, true);
    document.addEventListener('click', gesto, true);
  },

  CampoTextoWeb_Abrir: function (valorPtr, placeholderPtr, maxLen, senha) {
    var S = window.__ctw;
    if (!S) return;
    S.cfg = {
      valor: UTF8ToString(valorPtr),
      placeholder: UTF8ToString(placeholderPtr),
      maxLen: maxLen,
      senha: senha === 1
    };
    S.abrir();
  },

  CampoTextoWeb_Fechar: function () {
    if (window.__ctw) window.__ctw.fechar(false);
  }
});