import { nodeResolve } from '@rollup/plugin-node-resolve';

export default {
  input: 'worker.js',
  output: { file: 'dist/worker.js', format: 'es', sourcemap: true },
  plugins: [nodeResolve({ browser: true })]
};
