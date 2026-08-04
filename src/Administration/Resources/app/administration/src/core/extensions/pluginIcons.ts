import type { Component } from 'vue'
import {
  Activity,
  BarChart3,
  Bot,
  CreditCard,
  Database,
  Globe,
  Headphones,
  Mail,
  MessageSquare,
  Phone,
  Puzzle,
  Radio,
  ShoppingCart,
  Video,
} from 'lucide-vue-next'

/**
 * Maps the icon name a plugin declares in its admin navigation onto a shell
 * icon.
 *
 * Plugins ship their navigation as JSON and cannot hand over a component, so the
 * contract is a name from this vocabulary. An unknown name is not an error — it
 * falls back to the generic extension icon, which keeps a plugin from breaking
 * the sidebar by naming an icon we do not carry.
 */
const PLUGIN_ICONS: Record<string, Component> = {
  activity: Activity,
  bot: Bot,
  chart: BarChart3,
  database: Database,
  globe: Globe,
  headphones: Headphones,
  mail: Mail,
  message: MessageSquare,
  payment: CreditCard,
  phone: Phone,
  radio: Radio,
  shop: ShoppingCart,
  video: Video,
}

export function resolvePluginIcon(name: string | null): Component {
  if (!name) {
    return Puzzle
  }
  return PLUGIN_ICONS[name.toLowerCase()] ?? Puzzle
}
