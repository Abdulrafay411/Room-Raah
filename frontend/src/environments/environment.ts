export const environment = {
  production: true,
  apiBaseUrl: 'http://52.72.119.254:8080/api/v1',
  hubUrl: 'http://52.72.119.254:8080/hubs/chat',
  // The assistant's model runs on this machine under Ollama, and the browser calls it
  // directly - there is no server of ours in between.
  ollamaUrl: 'http://localhost:11434',
  agentModel: 'qwen3.5:9b',
};
