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
    text: 'Getting Started',
    items: [
      { text: 'Overview', link: '/guides/getting-started/' },
      { text: 'Build your first plugin', link: '/guides/getting-started/your-first-plugin' },
      { text: 'Install & Activate', link: '/guides/getting-started/install-activate' },
      { text: 'Project Layout', link: '/guides/getting-started/project-layout' },
      { text: 'The Plugin CLI', link: '/guides/getting-started/plugin-cli' },
    ],
  },
  {
    text: 'Plugin Fundamentals',
    items: [
      { text: 'Overview', link: '/guides/fundamentals/' },
      { text: 'The Plugin Entry', link: '/guides/fundamentals/plugin-entry' },
      { text: 'The registry.json Manifest', link: '/guides/fundamentals/registry-manifest' },
      { text: 'Dependency Injection', link: '/guides/fundamentals/dependency-injection' },
      { text: 'Exporting Extensions', link: '/guides/fundamentals/exporting-extensions' },
      { text: 'Plugin Configuration', link: '/guides/fundamentals/plugin-configuration' },
      { text: 'Plugin Dependencies', link: '/guides/fundamentals/plugin-dependencies' },
      { text: 'Compliance Metadata', link: '/guides/fundamentals/compliance-metadata' },
      { text: 'Best Practices', link: '/guides/fundamentals/best-practices' },
    ],
  },
  {
    text: 'Backend Extensions',
    items: [
      { text: 'Backend Extensions', link: '/guides/backend-extensions' },
      { text: 'Events & Jobs', link: '/guides/events-and-jobs' },
      { text: 'Capabilities & Entitlements', link: '/guides/capabilities' },
    ],
  },
  {
    text: 'Data Handling',
    items: [
      { text: 'Overview', link: '/guides/data/' },
      { text: 'Entities & Schemas', link: '/guides/data/entities-and-schemas' },
      { text: 'Migrations', link: '/guides/data/migrations' },
      { text: 'Custom Fields', link: '/guides/data/custom-fields' },
      { text: 'The Plugin Data Store', link: '/guides/data/data-store' },
      { text: 'Retention & GDPR', link: '/guides/data/retention-and-gdpr' },
      { text: 'Secrets', link: '/guides/data/secrets' },
    ],
  },
  {
    text: 'Automation',
    items: [
      { text: 'Overview', link: '/guides/automation/' },
      { text: 'Background Jobs', link: '/guides/automation/background-jobs' },
      { text: 'Recurring Jobs', link: '/guides/automation/recurring-jobs' },
      { text: 'Rules', link: '/guides/automation/rules' },
      { text: 'Flows', link: '/guides/automation/flows' },
      { text: 'Webhooks', link: '/guides/automation/webhooks' },
    ],
  },
  {
    text: 'Surface Extensions',
    items: [
      { text: 'Overview', link: '/guides/surface/' },
      { text: 'Build a Surface Plugin', link: '/guides/surface/building-a-surface-plugin' },
      { text: 'App vs. Islands', link: '/guides/surface/app-vs-islands' },
      { text: 'SSR Templates', link: '/guides/surface/ssr-templates' },
      { text: 'Themes & Tokens', link: '/guides/surface/themes-and-tokens' },
      { text: 'Media & Assets', link: '/guides/surface/media-and-assets' },
    ],
  },
  {
    text: 'Admin Extensions',
    items: [
      { text: 'Overview', link: '/guides/admin/' },
      { text: 'Slots', link: '/guides/admin/slots' },
      { text: 'Hooks', link: '/guides/admin/hooks' },
      { text: 'Service Overrides', link: '/guides/admin/service-overrides' },
      { text: 'Build an Admin Module', link: '/guides/admin/building-an-admin-module' },
    ],
  },
  {
    text: 'Ship It',
    items: [{ text: 'Testing & Publishing', link: '/guides/testing-and-publishing' }],
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

  // The surface docs quote Nunjucks/Twig templates, which use `{{ }}`. Move Vue's own
  // interpolation delimiters out of the way so those examples render literally instead
  // of being parsed as Vue expressions (we never use `{{ }}` for real interpolation).
  vue: {
    template: { compilerOptions: { delimiters: ['{[', ']}'] } },
  },

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
