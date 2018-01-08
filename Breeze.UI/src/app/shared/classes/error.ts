import { Log } from '../services/logger.service';
import { DialogOptions } from '../classes/dialog-options';

export class Error {
    static getMessage(error: any): string {
        return Error.getFirstError(error).message;
    }

    static getHelpUrl(error: any): string {
        return Error.getFirstError(error).additionalInfoUrl;
    }

    static getFirstError(error: any) {
        if (!error.json().errors || error.json().errors.length === 0) {
            return {};
        }

        return error.json().errors[0];
    }

    static toDialogOptions(error: any, optionalTitle: string): DialogOptions {
        if (!error) { return null; }

        return {
            title: optionalTitle,
            body: Error.getMessage(error),
            helpUrl: Error.getHelpUrl(error)
        };
    }

    static logError(error: any) {
        Log.error(error);
    }
}
