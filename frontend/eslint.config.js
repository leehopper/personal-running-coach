import js from '@eslint/js'
import tseslint from 'typescript-eslint'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import sonarjs from 'eslint-plugin-sonarjs'
import prettier from 'eslint-config-prettier'

export default [
  // `src/app/api/generated/**` is owned by the codegen pipeline (DEC-066 /
  // R-071). Linting it would force hand-edits that the next `npm run
  // codegen` would overwrite — and the drift gate would flag every such
  // edit. The drift gate is the canonical correctness signal for these
  // files; ESLint stays focused on hand-authored code.
  { ignores: ['dist', 'src/app/api/generated/**'] },
  js.configs.recommended,
  ...tseslint.configs.strict,
  sonarjs.configs.recommended,
  {
    plugins: {
      'react-hooks': reactHooks,
      'react-refresh': reactRefresh,
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
    },
  },
  prettier,
]
