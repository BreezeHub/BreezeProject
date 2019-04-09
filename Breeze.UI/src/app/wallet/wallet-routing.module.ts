import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { WalletComponent } from './wallet.component';
import { HistoryComponent } from './history/history.component';
import { DashboardComponent } from './dashboard/dashboard.component';
import { TumblebitComponent } from './tumblebit/tumblebit.component';

const routes: Routes = [
  { path: 'wallet', component: WalletComponent, children: [
    { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
    { path: 'dashboard', component: DashboardComponent, data: { shouldReuse: false } } ,
    { path: 'history', component: HistoryComponent, data: { shouldReuse: false } },
    { path: 'privacy', component: TumblebitComponent, data: { shouldReuse: true } },
  ]},
  { path: 'stratis-wallet', component: WalletComponent, children: [
    { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
    { path: 'dashboard', component: DashboardComponent, data: { shouldReuse: false } } ,
    { path: 'history', component: HistoryComponent, data: { shouldReuse: false } },
    { path: 'privacy', component: TumblebitComponent, data: { shouldReuse: true } },
  ]}
];

@NgModule({
  imports: [ RouterModule.forChild(routes) ],
  exports: [ RouterModule ]
})

export class WalletRoutingModule {}
