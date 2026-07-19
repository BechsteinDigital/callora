import { defineConfig } from 'vitepress'

// The Callora documentation site. Conceptual docs (Users / Developers / Reference /
// Maintainer) live here as Markdown, organised Diátaxis-style. The .NET API reference
// is generated separately by DocFX and served at /api/ — so links to it are external
// to this site and excluded from dead-link checking.
const developerSidebar = [
  {
    text: 'Concepts',
    items: [{ text: 'Architecture', link: '/concepts/architecture' }],
  },
  {
    text: 'Guides',
    items: [
      { text: 'Plugin Development', link: '/guides/plugin-development' },
      { text: 'Backend Extensions', link: '/guides/backend-extensions' },
      { text: 'Admin & Surface Extensions', link: '/guides/admin-extensions' },
      { text: 'Capabilities & Entitlements', link: '/guides/capabilities' },
      { text: 'Events & Jobs', link: '/guides/events-and-jobs' },
      { text: 'Testing & Publishing', link: '/guides/testing-and-publishing' },
    ],
  },
  {
    text: 'Reference',
    items: [
      { text: 'Overview', link: '/reference/' },
      { text: 'REST API', link: '/reference/rest-api' },
      { text: '.NET Contracts', link: '/reference/dotnet-contracts' },
      { text: 'Extension Manifests', link: '/reference/extension-manifests' },
    ],
  },
]

export default defineConfig({
  title: 'Callora',
  description: 'Documentation for the Callora domain-neutral plugin platform and its first-party plugins.',
  lang: 'en-US',
  cleanUrls: true,
  lastUpdated: true,

  // /api/ is the DocFX-generated .NET reference, served alongside this site — a valid
  // runtime path but not a VitePress page, so it must not fail the dead-link check.
  ignoreDeadLinks: [/^\/api\//],

  themeConfig: {
    nav: [
      { text: 'Users', link: '/users/' },
      { text: 'Developers', link: '/developers/' },
      { text: 'Reference', link: '/reference/' },
      { text: 'Maintainer', link: '/maintainer/' },
      { text: '.NET API', link: '/api/' },
    ],

    sidebar: {
      '/users/': [
        {
          text: 'User Guide',
          items: [
            { text: 'Overview', link: '/users/' },
            { text: 'Getting Started', link: '/users/getting-started' },
            { text: 'Administration', link: '/users/administration' },
            { text: 'Workspaces & Surfaces', link: '/users/workspaces-surfaces' },
            { text: 'Communication', link: '/users/communication' },
            { text: 'Flows', link: '/users/flows' },
            { text: 'Operations', link: '/users/operations' },
          ],
        },
      ],
      '/developers/': developerSidebar,
      '/concepts/': developerSidebar,
      '/guides/': developerSidebar,
      '/reference/': developerSidebar,
      '/maintainer/': [
        {
          text: 'Maintainer Guide',
          items: [
            { text: 'Overview', link: '/maintainer/' },
            { text: 'Repository Structure', link: '/maintainer/repository-structure' },
            { text: 'Build & Release', link: '/maintainer/build-and-release' },
            { text: 'Deployment', link: '/maintainer/deployment' },
            { text: 'Migration & Rollback', link: '/maintainer/migration-and-rollback' },
            { text: 'Security', link: '/maintainer/security' },
            { text: 'Runbooks', link: '/maintainer/runbooks' },
          ],
        },
      ],
    },

    search: { provider: 'local' },
    outline: { level: [2, 3] },
    socialLinks: [{ icon: 'github', link: 'https://github.com/BechsteinDigital/callora' }],
  },
})
