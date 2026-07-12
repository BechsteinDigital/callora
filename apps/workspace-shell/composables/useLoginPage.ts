export function useLoginPage() {
  const auth = useWorkspaceAuth();
  const route = useRoute();
  const { workspaceKey, workspaceName, publicPathPrefix } = useWorkspaceContext();

  const loginValue = ref("");
  const password = ref("");
  const pending = ref(false);
  const errorMessage = ref("");

  const basePath = computed(() => {
    const prefix = publicPathPrefix.value?.trim() || "/";
    return prefix === "/" ? "" : prefix.replace(/\/+$/, "");
  });

  async function submit(): Promise<void> {
    errorMessage.value = "";
    if (!loginValue.value.trim() || !password.value) {
      errorMessage.value = "Bitte E-Mail/Benutzername und Passwort eingeben.";
      return;
    }
    if (!workspaceKey.value) {
      errorMessage.value = "Dieser Bereich konnte keinem Workspace zugeordnet werden.";
      return;
    }

    pending.value = true;
    try {
      await auth.login({
        login: loginValue.value.trim(),
        password: password.value,
        workspaceKey: workspaceKey.value
      });

      await navigateTo(sanitizeReturnUrl(route.query.returnUrl, `${basePath.value}/dashboard`));
    } catch {
      errorMessage.value = "Anmeldung fehlgeschlagen. Bitte Zugangsdaten prüfen.";
    } finally {
      pending.value = false;
    }
  }

  return {
    workspaceName,
    loginValue,
    password,
    pending,
    errorMessage,
    submit
  };
}
