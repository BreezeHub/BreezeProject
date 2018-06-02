import { Subscription } from 'rxjs/Subscription';

export class CompositeDisposable {
    private _subscriptions: Set<Subscription> = new Set<Subscription>();
    constructor(private subscriptions: Subscription[]) {
        subscriptions.forEach(x => this.add(x));
    }
    add(subscription: Subscription): void {
        this._subscriptions.add(subscription);
    }
    unsubscribe() {
        this._subscriptions.forEach(x => x.unsubscribe());
    }
}
