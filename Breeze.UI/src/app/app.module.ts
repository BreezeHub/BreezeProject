import { NgModule } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouteReuseStrategy } from '@angular/router';

import { SharedModule } from './shared/shared.module';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { LoginComponent } from './login/login.component';
import { ConfirmDialogComponent } from './shared/components/confirm-dialog/confirm-dialog.component';
import { ConnectionModalComponent } from './shared/components/connection-modal/connection-modal.component';

import { ApiService } from './shared/services/api.service';
import { GlobalService } from './shared/services/global.service';
import { Log } from './shared/services/logger.service';
import { TumblebitService } from './wallet/tumblebit/tumblebit.service';
import { ModalService } from './shared/services/modal.service';
import { LicenseService } from './shared/services/license.service';

import { SendComponent } from './wallet/send/send.component';
import { SendConfirmationComponent } from './wallet/send/send-confirmation/send-confirmation.component';
import { ReceiveComponent } from './wallet/receive/receive.component';
import { TransactionDetailsComponent } from './wallet/transaction-details/transaction-details.component';
import { PasswordConfirmationComponent } from './wallet/tumblebit/password-confirmation/password-confirmation.component';
import { LogoutConfirmationComponent } from './wallet/logout-confirmation/logout-confirmation.component';
import { LicenseAgreementComponent } from './wallet/license-agreement/license-agreement.component';

import { CustomReuseStrategy } from './reuse-strategy';

@NgModule({
  imports: [
    AppRoutingModule,
    SharedModule
  ],
  declarations: [
    AppComponent,
    ConnectionModalComponent,
    ConfirmDialogComponent,
    LoginComponent,
    LogoutConfirmationComponent,
    PasswordConfirmationComponent,
    SendComponent,
    SendConfirmationComponent,
    ReceiveComponent,
    TransactionDetailsComponent,
    LicenseAgreementComponent
  ],
  entryComponents: [
    PasswordConfirmationComponent,
    ConnectionModalComponent,
    ConfirmDialogComponent,
    SendComponent,
    SendConfirmationComponent,
    ReceiveComponent,
    TransactionDetailsComponent,
    LogoutConfirmationComponent
  ],
  providers: [ ApiService, GlobalService, ModalService, Title, TumblebitService, LicenseService,
    { provide: RouteReuseStrategy, useClass: CustomReuseStrategy } ],
  bootstrap: [ AppComponent ]
})

export class AppModule { }
