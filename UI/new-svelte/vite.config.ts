import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig } from 'vitest/config';

export default defineConfig({
	plugins: [sveltekit()],
	test: {
		include: ['src/**/*.{test,spec}.{js,ts}']
	},
	css: {
		preprocessorOptions: {
			scss: {
				api: 'modern'
			}
		}
	},
	server: {
		proxy: {
			'/api': 'http://localhost:5008',
			'/socket': {
				target: 'ws://localhost:5008',
				ws: true
			}
		}
	}
});
