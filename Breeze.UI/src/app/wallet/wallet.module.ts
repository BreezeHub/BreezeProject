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

@NgModule({
  imports: [
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
    ConnectionProgressComponent
  ],
  exports: []
})

export class WalletModule { }
