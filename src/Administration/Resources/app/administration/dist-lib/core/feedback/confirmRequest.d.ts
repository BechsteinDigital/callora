/** What the operator is being asked to confirm. */
export interface ConfirmRequest {
    readonly title: string;
    /** The consequence, in plain language — what happens once they agree. */
    readonly description?: string;
    readonly confirmLabel?: string;
    readonly cancelLabel?: string;
    /** Destructive actions get the danger treatment on the confirm button. */
    readonly tone?: 'default' | 'danger';
}
//# sourceMappingURL=confirmRequest.d.ts.map