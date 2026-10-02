// ─────────────────────────────────────────────────────────────────────
// CampoTextoWebBridge.jslib
//
// Cria um <input> HTML invisível sobreposto ao canvas do Unity. Em
// WebGL mobile, o TMP_InputField (desenhado dentro do <canvas>) não
// consegue abrir o teclado virtual do navegador — só um elemento HTML
// <input> de verdade, focado DENTRO do mesmo gesto de toque, consegue.
//
// Fluxo:
//   C# (CampoTextoWebAtivador.OnSelect) chama CTW_Mostrar(...)
//   → cria/reposiciona o <input> oculto e dá focus() nele
//   → navegador abre o teclado
//   → cada tecla digitada dispara 'input' → SendMessage de volta pro Unity
//   → 'blur' (usuário tocou fora ou apertou Enter/OK) esconde o input
//     e avisa o Unity
//
// SETUP: coloque este arquivo em Assets/Plugins/WebGL/
// ─────────────────────────────────────────────────────────────────────

mergeInto(LibraryManager.library, {

    CTW_Mostrar: function (x, y, largura, altura, placeholderPtr, senhaFlag, textoInicialPtr, maxLength) {
        var placeholder = UTF8ToString(placeholderPtr);
        var textoInicial = UTF8ToString(textoInicialPtr);

        var canvas = document.querySelector('#unity-canvas');
        if (!canvas) return;

        var input = document.getElementById('campo-texto-web-oculto');
        if (!input) {
            input = document.createElement('input');
            input.id = 'campo-texto-web-oculto';
            input.autocomplete = 'off';
            input.autocapitalize = 'off';
            input.setAttribute('autocorrect', 'off');
            input.spellcheck = false;

            input.style.position = 'fixed';
            input.style.zIndex = '10000';
            input.style.border = 'none';
            input.style.outline = 'none';
            input.style.background = 'transparent';
            input.style.color = 'transparent';
            input.style.caretColor = '#ffffff';
            input.style.padding = '0';
            input.style.margin = '0';
            input.style.display = 'none';
            document.body.appendChild(input);

            input.addEventListener('input', function () {
                if (window.unityInstance) {
                    window.unityInstance.SendMessage(
                        'CampoTextoWebBridgeAtivo', 'OnTextoAlteradoWeb', input.value
                    );
                }
            });

            input.addEventListener('blur', function () {
                input.style.display = 'none';
                if (window.unityInstance) {
                    window.unityInstance.SendMessage(
                        'CampoTextoWebBridgeAtivo', 'OnPerdeuFocoWeb', ''
                    );
                }
            });

            // Enter/"Ir" no teclado mobile confirma e fecha
            input.addEventListener('keydown', function (e) {
                if (e.key === 'Enter') input.blur();
            });
        }

        var rect = canvas.getBoundingClientRect();
        var escalaX = rect.width / canvas.width;
        var escalaY = rect.height / canvas.height;

        input.style.left = (rect.left + x * escalaX) + 'px';
        input.style.top = (rect.top + y * escalaY) + 'px';
        input.style.width = (largura * escalaX) + 'px';
        input.style.height = (altura * escalaY) + 'px';
        input.style.fontSize = Math.max(16, altura * escalaY * 0.6) + 'px'; // 16px+ evita zoom automático no iOS

        input.placeholder = placeholder;
        input.type = senhaFlag === 1 ? 'password' : 'text';
        if (maxLength > 0) input.maxLength = maxLength;
        else input.removeAttribute('maxlength');
        input.value = textoInicial;

        input.style.display = 'block';
        input.focus();
    },

    CTW_AtualizarPosicao: function (x, y, largura, altura) {
        var canvas = document.querySelector('#unity-canvas');
        var input = document.getElementById('campo-texto-web-oculto');
        if (!canvas || !input) return;

        var rect = canvas.getBoundingClientRect();
        var escalaX = rect.width / canvas.width;
        var escalaY = rect.height / canvas.height;

        input.style.left = (rect.left + x * escalaX) + 'px';
        input.style.top = (rect.top + y * escalaY) + 'px';
        input.style.width = (largura * escalaX) + 'px';
        input.style.height = (altura * escalaY) + 'px';
    },

    CTW_Esconder: function () {
        var input = document.getElementById('campo-texto-web-oculto');
        if (input) {
            input.blur();
            input.style.display = 'none';
        }
    }

});
