import { NgModule } from '@angular/core';

import { WalletComponent } from './wallet.component';
import { MenuComponent } from './menu/menu.component';
import { DashboardComponent } from './dashboard/dashboard.component';
import { TumblebitComponent } from './tumblebit/tumblebit.component';
import { HistoryComponent } from './history/history.component';

import { WalletRoutingModule } from './wallet-routing.module';
import { SidebarComponent } from './sidebar/sidebar.component';
import { StatusBarComponent } from './status-bar/status-bar.component';
import { ConnectionProgressComponent } from './tumblebit/connection-progress/connection-progress.component';
import { SharedModule } from '../shared/shared.module';
import { LicenseAgreementComponent } from './license-agreement/license-agreement.component';
import { LogoutConfirmationComponent } from './logout-confirmation/logout-confirmation.component';
import { ReceiveComponent } from './receive/receive.component';
import { SendComponent } from './send/send.component';
import { SendConfirmationComponent } from './send/send-confirmation/send-confirmation.component';
import { TransactionDetailsComponent } from './transaction-details/transaction-details.component';

@NgModule({
  imports: [
    SharedModule,
    WalletRoutingModule
  ],
  declarations: [
    WalletComponent,
    MenuComponent,
    DashboardComponent,
    TumblebitComponent,
    HistoryComponent,
    SidebarComponent,
    StatusBarComponent,
    ConnectionProgressComponent,
    LicenseAgreementComponent,
    LogoutConfirmationComponent,
    ReceiveComponent,
    SendComponent,
    SendConfirmationComponent,
    TransactionDetailsComponent,
    TumblebitComponent
  ],
  entryComponents: [
    SendComponent,
    SendConfirmationComponent,
    ReceiveComponent,
    TransactionDetailsComponent,
    LogoutConfirmationComponent
  ]
})

export class WalletModule { }
