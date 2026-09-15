import { Routes } from '@angular/router';
import { JobsPageComponent } from './pages/jobs-page/jobs-page.component';

export const routes: Routes = [
  { path: '', component: JobsPageComponent },
  { path: '**', redirectTo: '' }
];
