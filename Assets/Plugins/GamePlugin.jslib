mergeInto(LibraryManager.library, {

  // Envio de resultado da fase para a plataforma
  EnviarResultadoFase: function(jsonPtr) {
    var json = UTF8ToString(jsonPtr);
    fetch("https://api.plataformamati.dev/auth/jogos/partida", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Authorization": "Bearer c6410303625a974a7c64158c241219792205f03e5f46c9873d2c03a3527512f6"
      },
      body: json
    })
    .then(function(res) {
      console.log("[GamePlugin] Resultado enviado. Status:", res.status);
    })
    .catch(function(err) {
      console.error("[GamePlugin] Erro ao enviar resultado:", err);
    });
  }

});
