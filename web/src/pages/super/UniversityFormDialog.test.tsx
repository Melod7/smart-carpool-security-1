import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { UniversityFormDialog } from './UniversityFormDialog'

describe('UniversityFormDialog', () => {
  it('muestra errores cuando nombre y slug están vacíos', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn()

    render(
      <UniversityFormDialog
        mode="create"
        onClose={() => undefined}
        onSubmit={onSubmit}
      />,
    )

    await user.click(screen.getByRole('button', { name: 'Crear' }))

    expect(screen.getByText('El nombre es obligatorio.')).toBeInTheDocument()
    expect(screen.getByText('El slug es obligatorio.')).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('envía el formulario cuando nombre y slug son válidos', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn()

    render(
      <UniversityFormDialog
        mode="create"
        onClose={() => undefined}
        onSubmit={onSubmit}
      />,
    )

    await user.type(screen.getByLabelText('Nombre'), 'Universidad Técnica')
    await user.type(screen.getByLabelText('Identificador'), 'utn')
    await user.click(screen.getByRole('button', { name: 'Crear' }))

    expect(onSubmit).toHaveBeenCalledWith({
      name: 'Universidad Técnica',
      slug: 'utn',
    })
  })
})
