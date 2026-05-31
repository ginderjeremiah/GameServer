import ts from 'typescript-eslint';
import svelte from 'eslint-plugin-svelte';
import prettier from 'eslint-config-prettier';
import globals from 'globals';
import importPlugin from 'eslint-plugin-import';

/** @type {import('eslint').Linter.FlatConfig[]} */
export default [
	...ts.configs.recommended,
	...svelte.configs['flat/recommended'],
	prettier,
	...svelte.configs['flat/prettier'],
	{
		languageOptions: {
			globals: {
				...globals.browser,
        ...globals.node
			}
		}
	},
	{
		files: ['**/*.svelte', '**/*.svelte.ts'],
		languageOptions: {
			parserOptions: {
				parser: ts.parser
			}
		}
	},
	{
		files: ['**/*.ts'],
		languageOptions: {
			ecmaVersion: 'latest',
			sourceType: 'module'
		},
    plugins: {
      import: importPlugin
    },
		rules: {
			'import/no-cycle': 'error'
		}
	},
	{
		ignores: ['build/', '.svelte-kit/', 'dist/']
	}
];
