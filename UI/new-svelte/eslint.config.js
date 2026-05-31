import ts from 'typescript-eslint';
import svelte from 'eslint-plugin-svelte';
import prettier from 'eslint-config-prettier';
import globals from 'globals';

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
	// {
	// 	files: ['**/*.{js,ts}'],
	// 	languageOptions: {
	// 		ecmaVersion: 'latest',
	// 		sourceType: 'module'
	// 	},
	// 	rules: {
	// 		'no-unused-vars': 'warn',
	// 		'import/no-dynamic-require': 'warn',
	// 		'import/no-nodejs-modules': 'warn',
	// 		'import/no-cycle': 'warn'
	// 	}
	// },
	{
		ignores: ['build/', '.svelte-kit/', 'dist/']
	}
];
