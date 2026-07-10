import { ChangeDetectionStrategy, Component } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-landing-page',
  imports: [RouterLink, ButtonModule, CardModule, TagModule],
  templateUrl: './landing-page.html',
  styleUrl: './landing-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LandingPage {}
