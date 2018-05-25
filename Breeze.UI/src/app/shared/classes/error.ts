import { Log } from '../services/logger.service';
import { DialogOptions } from '../classes/dialog-options';

export class Error {
    static getMessage(error: any): string {
      if (!error) {
        return null;
      }

      if (typeof error === 'string' || error instanceof String) {
        return error.toString();
      }

      const firstError = Error.getFirstError(error);

      // TODO: at the moment we have to check if detailed description contains word "Exception"
      // as fullnode code abuses Description property for both exception stacktrace and detailed messages
      // once this is fixed in fullnode this check can be removed. This has been discussed between
      // Igor and Carlton on 2018-05-23
      return !!firstError.description && !firstError.description.includes('Exception:')
                ? firstError.description
                : firstError.message;
    }

    static getHelpUrl(error: any): string {
        return Error.getFirstError(error).additionalInfoUrl;
    }

    static getFirstError(error: any) {
        if (!error.json().errors || error.json().errors.length === 0) {
            return { message: error.message };
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

    static toDialogOptionsWithFallbackMsg(error: any, optionalTitle: string, fallbackMessage: string): DialogOptions {
        if (!error) { return null; }

        return {
            title: optionalTitle,
            body: Error.getMessage(error) || fallbackMessage,
            helpUrl: Error.getHelpUrl(error)
        };
    }

    static logError(error: any) {
        Log.error(error);
    }
}
