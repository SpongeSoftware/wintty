import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

// On GitHub Actions, derive site/base from owner/repo so Pages paths are correct.
const repo = process.env.GITHUB_REPOSITORY; // "owner/name"
const [owner, name] = repo ? repo.split('/') : [undefined, undefined];

export default defineConfig({
  site: owner ? `https://${owner}.github.io` : undefined,
  base: name ? `/${name}` : undefined,
  integrations: [
    starlight({
      title: 'wintty',
      description: 'A Windows terminal built on libghostty-vt.',
      social: owner
        ? [{ icon: 'github', label: 'GitHub', href: `https://github.com/${repo}` }]
        : [],
      sidebar: [
        { label: 'Getting started', items: ['install', 'download'] },
        { label: 'Reference', items: ['configuration', 'themes', 'keybindings', 'architecture'] },
        { label: 'Project', items: ['engineering', 'contributing', 'changelog'] },
      ],
    }),
  ],
});
