<script setup lang="ts">
import { h, resolveComponent } from 'vue';
import type { TableColumn } from '@nuxt/ui';
import type {
  PluginAuditEntry,
  PluginContractCompatibility,
  PluginContractSupport,
  TrustedPluginSigner
} from '~/types/admin-plugins';

defineProps<{
  contractSupport: PluginContractSupport[];
  contractCompatibility: PluginContractCompatibility[];
  trustedSigners: TrustedPluginSigner[];
  auditEntries: PluginAuditEntry[];
  loading: boolean;
}>();

const UBadge = resolveComponent('UBadge');

function toLocalDateTime(value: string): string {
  return new Date(value).toLocaleString();
}

const auditColumns: TableColumn<PluginAuditEntry>[] = [{
  accessorKey: 'occurredAtUtc',
  header: 'Zeit',
  cell: ({ row }) => toLocalDateTime(row.original.occurredAtUtc)
}, {
  accessorKey: 'action',
  header: 'Aktion'
}, {
  accessorKey: 'pluginId',
  header: 'Plugin'
}, {
  accessorKey: 'isSuccess',
  header: 'Ergebnis',
  cell: ({ row }) => h(UBadge, {
    variant: 'subtle',
    color: row.original.isSuccess ? 'success' : 'error'
  }, () => row.original.isSuccess ? 'ok' : 'failed')
}, {
  accessorKey: 'requestedBy',
  header: 'User'
}];

const supportColumns: TableColumn<PluginContractSupport>[] = [{
  accessorKey: 'contractVersion',
  header: 'Contract'
}, {
  accessorKey: 'supportStatus',
  header: 'Status'
}, {
  accessorKey: 'isInstallable',
  header: 'Installable',
  cell: ({ row }) => h(UBadge, {
    variant: 'subtle',
    color: row.original.isInstallable ? 'success' : 'error'
  }, () => row.original.isInstallable ? 'yes' : 'no')
}, {
  accessorKey: 'emitsWarning',
  header: 'Warning',
  cell: ({ row }) => h(UBadge, {
    variant: 'subtle',
    color: row.original.emitsWarning ? 'warning' : 'neutral'
  }, () => row.original.emitsWarning ? 'yes' : 'no')
}, {
  accessorKey: 'message',
  header: 'Message'
}];

const compatibilityColumns: TableColumn<PluginContractCompatibility>[] = [{
  accessorKey: 'contractVersion',
  header: 'Contract'
}, {
  accessorKey: 'result',
  header: 'Result'
}, {
  accessorKey: 'isCompatible',
  header: 'Compatible',
  cell: ({ row }) => h(UBadge, {
    variant: 'subtle',
    color: row.original.isCompatible ? 'success' : 'error'
  }, () => row.original.isCompatible ? 'yes' : 'no')
}, {
  accessorKey: 'hostVersion',
  header: 'Host'
}, {
  accessorKey: 'coreVersion',
  header: 'Core'
}];

const trustedSignerColumns: TableColumn<TrustedPluginSigner>[] = [{
  accessorKey: 'displayName',
  header: 'Signer'
}, {
  accessorKey: 'publisherId',
  header: 'Publisher'
}, {
  accessorKey: 'thumbprint',
  header: 'Thumbprint'
}, {
  accessorKey: 'source',
  header: 'Source'
}];
</script>

<template>
  <div class="space-y-6">
    <div class="grid grid-cols-1 xl:grid-cols-2 gap-4">
      <UPageCard title="Contract Support">
        <UTable :data="contractSupport" :columns="supportColumns" :loading="loading" />
      </UPageCard>

      <UPageCard title="Contract Compatibility">
        <UTable :data="contractCompatibility" :columns="compatibilityColumns" :loading="loading" />
      </UPageCard>
    </div>

    <div class="grid grid-cols-1 xl:grid-cols-2 gap-4">
      <UPageCard title="Trusted Signers">
        <UTable :data="trustedSigners" :columns="trustedSignerColumns" :loading="loading" />
      </UPageCard>

      <UPageCard title="Audit Log (letzte 100)">
        <UTable :data="auditEntries" :columns="auditColumns" :loading="loading" />
      </UPageCard>
    </div>
  </div>
</template>
