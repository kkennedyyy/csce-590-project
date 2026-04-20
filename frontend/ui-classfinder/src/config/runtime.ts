interface RuntimeConfig {
  apiBaseUrl: string;
}

export const runtimeConfig: RuntimeConfig = {
  apiBaseUrl: 'http://localhost:8080',
};

export function setRuntimeConfig(next: Partial<RuntimeConfig>): void {
  Object.assign(runtimeConfig, next);
}
