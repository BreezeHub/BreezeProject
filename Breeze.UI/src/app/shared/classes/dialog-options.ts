import { NgbModalOptions } from '@ng-bootstrap/ng-bootstrap';

export interface DialogOptions {
    title?: string;
    body?: string;
    helpUrl?: string;
    showHomeLink?: boolean;
    modalOptions?: NgbModalOptions;
};
