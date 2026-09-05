mergeInto(LibraryManager.library, {

  // ─────────────────────────────────────────────────────────────────
  // Lê o nome do aluno da URL da página, do mesmo jeito que o token
  // já é lido no topo do arquivo (var tokenPlataforma = ...).
  // Exemplo de URL esperada: seusite.com/jogo?t=abc123&nome=Joao
  //
  // Se o parâmetro "nome" não vier na URL (ex: testando local sem o site),
  // devolve "Jogador" como fallback, pra não travar nada.
  // ─────────────────────────────────────────────────────────────────
  GetNomeDaURL: function () {
    var nome = new URLSearchParams(location.search).get('nome') || "Jogador";
    var bufferSize = lengthBytesUTF8(nome) + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(nome, buffer, bufferSize);
    return buffer;
  },

  // Envio de resultado da fase para a plataforma (rota antiga)
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
  },

  // Envio por questão (rota já existente — alimenta a planilha antiga, sem mudanças)
  EnviarQuestao: function(jsonPtr) {
    var json = UTF8ToString(jsonPtr);
    var obj = JSON.parse(json);
    obj.token = new URLSearchParams(location.search).get('t');
    fetch("https://api.plataformamati.dev/auth/jogos/questao", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Authorization": "Bearer c6410303625a974a7c64158c241219792205f03e5f46c9873d2c03a3527512f6"
      },
      body: JSON.stringify(obj)
    })
    .then(function(res) {
      console.log("[GamePlugin] Questao enviada. Status:", res.status);
    })
    .catch(function(err) {
      console.error("[GamePlugin] Erro ao enviar questao:", err);
    });
  },

  // Envio do relatório final da partida (novo — alimenta a planilha nova)
  EnviarRelatorio: function(jsonPtr) {
    var json = UTF8ToString(jsonPtr);
    var obj = JSON.parse(json);
    obj.token = new URLSearchParams(location.search).get('t');
    fetch("https://api.plataformamati.dev/auth/jogos/relatorio", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Authorization": "Bearer c6410303625a974a7c64158c241219792205f03e5f46c9873d2c03a3527512f6"
      },
      body: JSON.stringify(obj)
    })
    .then(function(res) {
      console.log("[GamePlugin] Relatorio final enviado. Status:", res.status);
    })
    .catch(function(err) {
      console.error("[GamePlugin] Erro ao enviar relatorio:", err);
    });
  }

});