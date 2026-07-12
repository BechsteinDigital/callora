<script setup lang="ts">
import type {
  InstallLocalPluginRequest,
  InstallNuGetPluginRequest,
  PluginLifecycleApiResponse
} from '~/types/admin-plugins';

type InstallSource = 'local' | 'nuget' | 'assembly' | 'zip';

const confirmOpen = defineModel<boolean>('confirmOpen', { required: true });
const installOpen = defineModel<boolean>('installOpen', { required: true });
const updateOpen = defineModel<boolean>('updateOpen', { required: true });

const props = defineProps<{
  updatePluginId: string;
}>();

const emit = defineEmits<{
  completed: [];
}>();

const auth = useAdminAuth();
const { request } = useAdminApi();

const installSource = ref<InstallSource>('local');
const installError = ref<string | null>(null);
const installInfo = ref<string | null>(null);
const installPending = ref(false);
const updateNuGetError = ref<string | null>(null);
const updateNuGetPending = ref(false);
const selectedZipFile = ref<File | null>(null);

const localState = reactive({
  pluginId: '',
  buildIfNeeded: true,
  forceBuild: false
});

const nugetState = reactive({
  packageId: '',
  packageVersion: '',
  assemblyFileName: '',
  entryTypeName: ''
});

const assemblyState = reactive({
  assemblyPath: '',
  entryTypeName: ''
});

const updateNuGetState = reactive({
  pluginId: '',
  packageId: '',
  packageVersion: '',
  assemblyFileName: '',
  entryTypeName: ''
});

const installSourceOptions = [{
  label: 'Lokales Plugin',
  value: 'local'
}, {
  label: 'NuGet-Paket',
  value: 'nuget'
}, {
  label: 'Assembly-Pfad',
  value: 'assembly'
}, {
  label: 'ZIP-Datei',
  value: 'zip'
}];

const selectedZipFileName = computed(() => selectedZipFile.value?.name || 'Keine Datei ausgewählt');

function extractErrorMessage(error: unknown, fallback: string): string {
  const payload = (error as { data?: { message?: unknown } } | null)?.data;
  if (payload && typeof payload.message === 'string' && payload.message.trim().length > 0) {
    return payload.message;
  }

  return fallback;
}

function resetInstallState(): void {
  installSource.value = 'local';
  installError.value = null;
  installInfo.value = null;
  selectedZipFile.value = null;
  localState.pluginId = '';
  localState.buildIfNeeded = true;
  localState.forceBuild = false;
  nugetState.packageId = '';
  nugetState.packageVersion = '';
  nugetState.assemblyFileName = '';
  nugetState.entryTypeName = '';
  assemblyState.assemblyPath = '';
  assemblyState.entryTypeName = '';
}

function openInstallModal(): void {
  confirmOpen.value = false;
  installOpen.value = true;
}

function onZipFileChanged(event: Event): void {
  const target = event.target as HTMLInputElement | null;
  selectedZipFile.value = target?.files?.[0] ?? null;
}

async function submitLifecycleRequest(
  path: string,
  body: Record<string, unknown>,
  fallbackError: string
): Promise<boolean> {
  installPending.value = true;
  try {
    const result = await request<PluginLifecycleApiResponse>(path, {
      method: 'POST',
      body
    });

    if (!result.isSuccess) {
      installError.value = result.message || fallbackError;
      return false;
    }

    installOpen.value = false;
    resetInstallState();
    emit('completed');
    return true;
  } catch (error) {
    installError.value = extractErrorMessage(error, fallbackError);
    return false;
  } finally {
    installPending.value = false;
  }
}

async function submitInstall(): Promise<void> {
  installError.value = null;
  installInfo.value = null;

  if (installSource.value === 'local') {
    const pluginId = localState.pluginId.trim();
    if (!pluginId) {
      installError.value = 'Plugin ID ist erforderlich.';
      return;
    }

    const payload: InstallLocalPluginRequest = {
      pluginId,
      buildIfNeeded: localState.buildIfNeeded,
      forceBuild: localState.forceBuild,
      requestedBy: auth.session.value?.userId || null
    };

    await submitLifecycleRequest('/api/plugins/install/local', payload as unknown as Record<string, unknown>, 'Lokale Installation fehlgeschlagen.');
    return;
  }

  if (installSource.value === 'assembly') {
    const assemblyPath = assemblyState.assemblyPath.trim();
    if (!assemblyPath) {
      installError.value = 'Assembly-Pfad ist erforderlich.';
      return;
    }

    await submitLifecycleRequest('/api/plugins/install', {
      assemblyPath,
      entryTypeName: assemblyState.entryTypeName.trim() || null,
      requestedBy: auth.session.value?.userId || null
    }, 'Assembly-Installation fehlgeschlagen.');
    return;
  }

  if (installSource.value === 'zip') {
    if (!selectedZipFile.value) {
      installError.value = 'Bitte eine ZIP-Datei auswählen.';
      return;
    }

    installInfo.value = `ZIP-Upload vorbereitet (${selectedZipFile.value.name}). Die eigentliche Installation wird im nächsten Schritt implementiert.`;
    return;
  }

  const packageId = nugetState.packageId.trim();
  const packageVersion = nugetState.packageVersion.trim();

  if (!packageId || !packageVersion) {
    installError.value = 'Package ID und Version sind erforderlich.';
    return;
  }

  const payload: InstallNuGetPluginRequest = {
    packageId,
    packageVersion,
    assemblyFileName: nugetState.assemblyFileName.trim() || null,
    entryTypeName: nugetState.entryTypeName.trim() || null,
    requestedBy: auth.session.value?.userId || null
  };

  await submitLifecycleRequest('/api/plugins/install/nuget', payload as unknown as Record<string, unknown>, 'NuGet-Installation fehlgeschlagen.');
}

async function submitNuGetUpdate(): Promise<void> {
  updateNuGetError.value = null;

  const pluginId = updateNuGetState.pluginId.trim();
  const packageId = updateNuGetState.packageId.trim();
  const packageVersion = updateNuGetState.packageVersion.trim();

  if (!pluginId || !packageId || !packageVersion) {
    updateNuGetError.value = 'Plugin ID, Package ID und Version sind erforderlich.';
    return;
  }

  updateNuGetPending.value = true;

  try {
    const result = await request<PluginLifecycleApiResponse>(`/api/plugins/${encodeURIComponent(pluginId)}/update/nuget`, {
      method: 'POST',
      body: {
        packageId,
        packageVersion,
        assemblyFileName: updateNuGetState.assemblyFileName.trim() || null,
        entryTypeName: updateNuGetState.entryTypeName.trim() || null,
        requestedBy: auth.session.value?.userId || null
      }
    });

    if (!result.isSuccess) {
      updateNuGetError.value = result.message || 'NuGet-Update fehlgeschlagen.';
      return;
    }

    updateOpen.value = false;
    emit('completed');
  } catch (error) {
    updateNuGetError.value = extractErrorMessage(error, 'NuGet-Update fehlgeschlagen.');
  } finally {
    updateNuGetPending.value = false;
  }
}

watch(installOpen, (isOpen) => {
  if (!isOpen) {
    resetInstallState();
  }
});

watch(updateOpen, (isOpen) => {
  if (!isOpen) {
    updateNuGetError.value = null;
    return;
  }

  updateNuGetState.pluginId = props.updatePluginId;
  updateNuGetState.packageId = props.updatePluginId;
  updateNuGetState.packageVersion = '';
  updateNuGetState.assemblyFileName = '';
  updateNuGetState.entryTypeName = '';
});
</script>

<template>
  <UModal
    v-model:open="confirmOpen"
    title="Warnung"
    :ui="{ footer: 'justify-end gap-2' }"
  >
    <template #body>
      <p class="text-sm text-muted">
        Erweiterungen, die nicht aus dem Callora-Store stammen, werden nicht automatisch verifiziert. Nur vertrauenswürdige Quellen verwenden.
      </p>
    </template>
    <template #footer>
      <UButton color="neutral" variant="ghost" @click="confirmOpen = false">
        Abbrechen
      </UButton>
      <UButton color="primary" @click="openInstallModal">
        Bestätigen
      </UButton>
    </template>
  </UModal>

  <UModal
    v-model:open="installOpen"
    title="Plugin installieren"
    :ui="{ footer: 'justify-end gap-2' }"
  >
    <template #body>
      <div class="space-y-4">
        <UAlert
          v-if="installError"
          color="error"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          :title="installError"
        />
        <UAlert
          v-if="installInfo"
          color="info"
          variant="subtle"
          icon="i-lucide-info"
          :title="installInfo"
        />

        <UFormField label="Quelle">
          <USelect
            v-model="installSource"
            class="w-full"
            :items="installSourceOptions"
          />
        </UFormField>

        <div v-if="installSource === 'local'" class="space-y-3">
          <UFormField label="Plugin ID" required>
            <UInput v-model="localState.pluginId" class="w-full" placeholder="template-alpha" />
          </UFormField>
          <UFormField label="Kompilierung">
            <UCheckbox
              v-model="localState.buildIfNeeded"
              label="Automatisch kompilieren, wenn keine DLL vorhanden ist"
            />
          </UFormField>
          <UFormField label="Rebuild">
            <UCheckbox
              v-model="localState.forceBuild"
              label="Neu kompilieren erzwingen (no-incremental)"
            />
          </UFormField>
        </div>

        <div v-else-if="installSource === 'nuget'" class="space-y-3">
          <UFormField label="Package ID" required>
            <UInput v-model="nugetState.packageId" class="w-full" placeholder="Callora.Plugin.Example" />
          </UFormField>
          <UFormField label="Version" required>
            <UInput v-model="nugetState.packageVersion" class="w-full" placeholder="1.0.0" />
          </UFormField>
          <UFormField label="Assembly-Dateiname (optional)">
            <UInput v-model="nugetState.assemblyFileName" class="w-full" placeholder="Callora.Plugin.Example.dll" />
          </UFormField>
          <UFormField label="Entry Type (optional)">
            <UInput v-model="nugetState.entryTypeName" class="w-full" placeholder="Example.Plugin.EntryPoint" />
          </UFormField>
        </div>

        <div v-else-if="installSource === 'assembly'" class="space-y-3">
          <UFormField label="Assembly Pfad" required>
            <UInput v-model="assemblyState.assemblyPath" class="w-full" placeholder="/app/custom/plugins/MyPlugin/bin/Release/net8.0/MyPlugin.dll" />
          </UFormField>
          <UFormField label="Entry Type (optional)">
            <UInput v-model="assemblyState.entryTypeName" class="w-full" placeholder="Example.Plugin.EntryPoint" />
          </UFormField>
        </div>

        <div v-else class="space-y-3">
          <UFormField label="ZIP-Datei" required>
            <UInput
              type="file"
              accept=".zip,application/zip,application/x-zip-compressed"
              class="w-full"
              @change="onZipFileChanged"
            />
          </UFormField>
          <UFormField label="Ausgewählte Datei">
            <UInput :model-value="selectedZipFileName" disabled />
          </UFormField>
          <UAlert
            color="warning"
            variant="subtle"
            icon="i-lucide-construction"
            title="ZIP-Installation wird im nächsten Schritt serverseitig aktiviert."
          />
        </div>
      </div>
    </template>
    <template #footer>
      <UButton color="neutral" variant="ghost" @click="installOpen = false">
        Schließen
      </UButton>
      <UButton color="primary" :loading="installPending" @click="submitInstall">
        {{ installSource === 'zip' ? 'Vorbereiten' : 'Installieren' }}
      </UButton>
    </template>
  </UModal>

  <UModal
    v-model:open="updateOpen"
    title="Plugin per NuGet aktualisieren"
    :ui="{ footer: 'justify-end gap-2' }"
  >
    <template #body>
      <div class="space-y-4">
        <UAlert
          v-if="updateNuGetError"
          color="error"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          :title="updateNuGetError"
        />

        <UFormField label="Plugin ID" required>
          <UInput v-model="updateNuGetState.pluginId" class="w-full" disabled />
        </UFormField>
        <UFormField label="Package ID" required>
          <UInput v-model="updateNuGetState.packageId" class="w-full" />
        </UFormField>
        <UFormField label="Version" required>
          <UInput v-model="updateNuGetState.packageVersion" class="w-full" placeholder="1.0.1" />
        </UFormField>
        <UFormField label="Assembly-Dateiname (optional)">
          <UInput v-model="updateNuGetState.assemblyFileName" class="w-full" placeholder="Callora.Plugin.Example.dll" />
        </UFormField>
        <UFormField label="Entry Type (optional)">
          <UInput v-model="updateNuGetState.entryTypeName" class="w-full" placeholder="Example.Plugin.EntryPoint" />
        </UFormField>
      </div>
    </template>
    <template #footer>
      <UButton color="neutral" variant="ghost" @click="updateOpen = false">
        Abbrechen
      </UButton>
      <UButton color="primary" :loading="updateNuGetPending" @click="submitNuGetUpdate">
        Aktualisieren
      </UButton>
    </template>
  </UModal>
</template>
