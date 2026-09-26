import { createApp } from 'vue'
import PrimeVue from 'primevue/config'
import Aura from '@primeuix/themes/aura'
import Button from 'primevue/button'
import Card from 'primevue/card'
import Checkbox from 'primevue/checkbox'
import Dialog from 'primevue/dialog'
import Fieldset from 'primevue/fieldset'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Panel from 'primevue/panel'
import ProgressBar from 'primevue/progressbar'
import Select from 'primevue/select'
import SelectButton from 'primevue/selectbutton'
import Slider from 'primevue/slider'
import Tag from 'primevue/tag'
import Textarea from 'primevue/textarea'
import Toolbar from 'primevue/toolbar'
import Tooltip from 'primevue/tooltip'
import App from './App.vue'
import { locale } from './i18n'
import './style.css'

const app = createApp(App)
app.use(PrimeVue, {
  theme: {
    preset: Aura,
    // PrimeVue's styles go into their own cascade layer, so Tailwind utilities (a later layer) can override them.
    // The order itself is declared at the top of style.css.
    options: { cssLayer: { name: 'primevue', order: 'theme, base, primevue, components, utilities' } },
  },
  // A Button rendered as a link (as="a") keeps the browser's underline, since Tailwind runs without preflight and
  // the preset sets no text-decoration. Every Button looks the same, whatever its tag.
  // A Panel's content sits in a grid (for its collapse animation) whose item keeps its content's width, so a wide table
  // in it would widen the page on a phone instead of scrolling in its own wrapper.
  pt: { button: { root: { class: 'no-underline' } }, panel: { contentWrapper: { class: 'min-w-0' } } },
})
app
  .component('Button', Button)
  .component('Card', Card)
  .component('Checkbox', Checkbox)
  .component('Dialog', Dialog)
  .component('Fieldset', Fieldset)
  .component('InputNumber', InputNumber)
  .component('InputText', InputText)
  .component('Message', Message)
  .component('Panel', Panel)
  .component('ProgressBar', ProgressBar)
  .component('Select', Select)
  .component('SelectButton', SelectButton)
  .component('Slider', Slider)
  .component('Tag', Tag)
  .component('Textarea', Textarea)
  .component('Toolbar', Toolbar)
app.directive('tooltip', Tooltip)

document.documentElement.lang = locale.value
app.mount('#app')
