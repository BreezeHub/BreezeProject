import { RouteReuseStrategy, DetachedRouteHandle } from '@angular/router';
import { ActivatedRoute, ActivatedRouteSnapshot } from '@angular/router';

export class CustomReuseStrategy implements RouteReuseStrategy {

    handlers: { [key: string]: DetachedRouteHandle } = {};
  
    shouldDetach(route: ActivatedRouteSnapshot): boolean {
      return route.data.shouldReuse || false;
    }
  
    store(route: ActivatedRouteSnapshot, handle: {}): void {
      if (route.data.shouldReuse) {
        this.handlers[route.routeConfig.path] = handle;
      }
    }
  
    shouldAttach(route: ActivatedRouteSnapshot): boolean {
      return !!route.routeConfig && !!this.handlers[route.routeConfig.path];
    }
  
    retrieve(route: ActivatedRouteSnapshot): {} {
      if (!route.routeConfig) return null;
      return this.handlers[route.routeConfig.path];
    }
  
    shouldReuseRoute(future: ActivatedRouteSnapshot, curr: ActivatedRouteSnapshot): boolean {
      return future.data.shouldReuse || false;
    }
  
  }
  