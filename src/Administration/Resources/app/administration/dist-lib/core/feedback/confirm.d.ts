import { type ComputedRef } from 'vue';
import type { ConfirmRequest } from './confirmRequest';
export declare function confirm(request: ConfirmRequest): Promise<boolean>;
export declare function useConfirmDialog(): {
    current: ComputedRef<ConfirmRequest | null>;
    answer: (confirmed: boolean) => void;
};
/** Rejects every open request as "cancelled" — for tests, and on route teardown. */
export declare function resetConfirm(): void;
//# sourceMappingURL=confirm.d.ts.map