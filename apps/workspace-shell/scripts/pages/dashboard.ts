export default defineComponent({
  name: "DashboardPage",
  async setup() {
    const page = useDashboardPage();
    await page.refreshStatus();
    return page;
  }
});
