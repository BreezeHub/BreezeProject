import { Observable, of } from 'rxjs';
import { delay } from 'rxjs/operators';
import { Error } from '../../shared/classes/error';

export class ServiceShared
{
    public static onRetryWhen(errors) {
        return errors.mergeMap((error: any) => {
          const firstError = Error.getFirstError(error);
          if (error.status  === 0) {
            return of(error.status).pipe(delay(5000));
          }
          else if (error.status === 400 && !firstError.description) {
            console.log("Retrying; MVC error.");
            return of(error.status).pipe(delay(5000));
          }
          return Observable.throw(error);
       })
       .take(5)
       .concat(Observable.throw(errors));
      }
}
