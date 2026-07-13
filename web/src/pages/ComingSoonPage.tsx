type ComingSoonPageProps = {
  title: string
  description?: string
}

export function ComingSoonPage({ title, description }: ComingSoonPageProps) {
  return (
    <div>
      <h1 className="text-2xl font-semibold text-slate-900">{title}</h1>
      <p className="mt-2 text-slate-600">
        {description ?? 'Próximamente'}
      </p>
    </div>
  )
}
