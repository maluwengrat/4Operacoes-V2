mergeInto(LibraryManager.library, {

  MostrarGameOver: function(score) {
    if (window.unityBridge && window.unityBridge.mostrarGameOver) {
      window.unityBridge.mostrarGameOver(score);
    }
  },

  MostrarFaseCompleta: function(fase, aprovado) {
    if (window.unityBridge && window.unityBridge.mostrarFaseCompleta) {
      window.unityBridge.mostrarFaseCompleta(fase, aprovado);
    }
  },

  EsconderPaineis: function() {
    if (window.unityBridge && window.unityBridge.esconderPaineis) {
      window.unityBridge.esconderPaineis();
    }
  },

  MostrarMenuPrincipal: function() {
    if (window.unityBridge && window.unityBridge.mostrarMenuPrincipal) {
      window.unityBridge.mostrarMenuPrincipal();
    }
  },

  AtualizarHUD: function(scorePtr, fasePtr, timerPtr, perguntaPtr) {
    var score = UTF8ToString(scorePtr);
    var fase = UTF8ToString(fasePtr);
    var timer = UTF8ToString(timerPtr);
    var pergunta = UTF8ToString(perguntaPtr);
    if (window.unityBridge && window.unityBridge.atualizarHUD) {
      window.unityBridge.atualizarHUD(score, fase, timer, pergunta);
    }
  }

});