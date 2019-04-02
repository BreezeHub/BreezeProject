import { NgModule } from '@angular/core';
import { RouteReuseStrategy } from '@angular/router';
import { HTTP_INTERCEPTORS, HttpClientModule } from '@angular/common/http';

import { SharedModule } from './shared/shared.module';
import { AppRoutingModule } from './app-routing.module';
import { BrowserModule } from '@angular/platform-browser';
import { SetupModule } from './setup/setup.module';
import { WalletModule } from './wallet/wallet.module';

import { AppComponent } from './app.component';
import { LoginComponent } from './login/login.component';

import { CustomReuseStrategy } from './shared/reuse-strategy/reuse-strategy';
import { ApiInterceptor } from './shared/http-interceptors/api-interceptor';

@NgModule({
  imports: [
    BrowserModule,
    HttpClientModule,
    SharedModule,
    SetupModule,
    WalletModule,
    AppRoutingModule
  ],
  declarations: [
    AppComponent,
    LoginComponent
  ],
  providers: [
    { provide: RouteReuseStrategy, useClass: CustomReuseStrategy },
    { provide: HTTP_INTERCEPTORS, useClass: ApiInterceptor, multi: true}
  ],
  bootstrap: [ AppComponent ]
})

export class AppModule { }
