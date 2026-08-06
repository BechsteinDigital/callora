import { type Ref } from 'vue';
import type { ToastTone } from './toast';
/**
 * Transient feedback about something that just happened ("Plugin aktiviert",
 * "Speichern fehlgeschlagen"). It is deliberately separate from CalAlert: an
 * alert describes the state of the content in front of you, a toast reports the
 * outcome of an action and then disappears.
 */
export interface Toast {
    readonly id: number;
    readonly tone: ToastTone;
    readonly message: string;
    readonly description?: string;
}
export declare function useToasts(): {
    toasts: Ref<Toast[]>;
    dismiss: (id: number) => void;
};
/**
 * The reporting surface used from anywhere — stores, API layers, views. Kept as
 * plain functions rather than a composable so a non-component module can call it.
 */
export declare const toast: {
    success: (message: string, description?: string) => number;
    info: (message: string, description?: string) => number;
    warning: (message: string, description?: string) => number;
    /** Accepts an Error directly — the common case in a catch block. */
    error: (error: unknown, description?: string) => number;
};
/** Clears every toast and its timer — for tests, and on logout. */
export declare function resetToasts(): void;
//# sourceMappingURL=toasts.d.ts.map