import { defineConfig } from "vite";

// Библиотечная сборка: один UMD-файл с глобальным SupportGate. Игра подключает
// его тегом script, поэтому ни ESM, ни внешних зависимостей здесь быть не должно.
export default defineConfig(({ mode }) => ({
  define: {
    // Отладочный лог в прод-сборку не попадает: константа схлопывается, и весь
    // код под ней вырезается минификатором.
    __SG_DEBUG__: JSON.stringify(mode !== "production"),
  },
  build: {
    outDir: "dist",
    emptyOutDir: true,
    sourcemap: mode !== "production",
    minify: mode === "production",
    lib: {
      entry: "src/index.ts",
      name: "SupportGate",
      formats: ["umd"],
      fileName: () => "support_gate.js",
    },
  },
}));
