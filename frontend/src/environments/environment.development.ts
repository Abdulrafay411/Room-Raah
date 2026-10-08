export const environment = {
  production: false,
  apiBaseUrl: 'http://54.161.213.189:8080/api/v1',
  hubUrl: 'http://54.161.213.189:8080/hubs/chat',
  // The assistant's model runs on this machine under Ollama, and the browser calls it
  // directly - there is no server of ours in between.
  ollamaUrl: 'http://localhost:11434',
  agentModel: 'qwen3.5:9b',
};
