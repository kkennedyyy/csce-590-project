interface RuntimeConfig {
  apiBaseUrl: string;
}

export const runtimeConfig: RuntimeConfig = {
  apiBaseUrl: '',
};

export function setRuntimeConfig(next: Partial<RuntimeConfig>): void {
  Object.assign(runtimeConfig, next);
}
