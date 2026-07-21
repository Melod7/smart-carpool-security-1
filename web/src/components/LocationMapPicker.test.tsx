import { useState, type PropsWithChildren } from 'react'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { LocationMapPicker, type MapLatLng } from './LocationMapPicker'

const geocode = vi.hoisted(() => vi.fn())

vi.mock('@react-google-maps/api', () => ({
  useJsApiLoader: () => ({ isLoaded: true, loadError: undefined }),
  GoogleMap: ({ children }: PropsWithChildren) => <div data-testid="google-map">{children}</div>,
  Marker: () => null,
}))

function CampusForm({ onSubmit }: { onSubmit: () => void }) {
  const [name, setName] = useState('')
  const [address, setAddress] = useState('')
  const [point, setPoint] = useState<MapLatLng | null>(null)

  return (
    <form
      onSubmit={(event) => {
        event.preventDefault()
        onSubmit()
      }}
    >
      <label>
        Nombre del campus
        <input value={name} onChange={(event) => setName(event.target.value)} />
      </label>
      <LocationMapPicker
        apiKey="maps-test-key"
        value={point}
        onChange={setPoint}
        address={address}
        onAddressChange={setAddress}
      />
    </form>
  )
}

describe('LocationMapPicker', () => {
  beforeEach(() => {
    geocode.mockReset()
    geocode.mockResolvedValue({
      results: [
        {
          formatted_address: 'Av. 17 de Julio, Ibarra, Ecuador',
          geometry: {
            location: {
              lat: () => 0.358,
              lng: () => -78.111,
            },
          },
        },
      ],
    })

    Object.defineProperty(globalThis, 'google', {
      configurable: true,
      value: {
        maps: {
          Geocoder: class {
            geocode = geocode
          },
          SymbolPath: { CIRCLE: 0 },
        },
      },
    })
  })

  afterEach(() => {
    Reflect.deleteProperty(globalThis, 'google')
  })

  it('buscar una dirección no envía ni reinicia el formulario del campus', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn()
    render(<CampusForm onSubmit={onSubmit} />)

    const nameInput = screen.getByLabelText('Nombre del campus')
    const addressInput = screen.getByLabelText('Dirección del campus')
    await user.type(nameInput, 'Campus El Olivo')
    await user.type(addressInput, 'UTN Ibarra')
    await user.click(screen.getByRole('button', { name: 'Buscar' }))

    await waitFor(() => expect(geocode).toHaveBeenCalled())
    expect(onSubmit).not.toHaveBeenCalled()
    expect(nameInput).toHaveValue('Campus El Olivo')
    expect(addressInput).toHaveValue('Av. 17 de Julio, Ibarra, Ecuador')
  })

  it('presionar Enter busca sin enviar el formulario del campus', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn()
    render(<CampusForm onSubmit={onSubmit} />)

    const addressInput = screen.getByLabelText('Dirección del campus')
    await user.type(addressInput, 'UTN Ibarra{enter}')

    await waitFor(() => expect(geocode).toHaveBeenCalled())
    expect(onSubmit).not.toHaveBeenCalled()
  })
})
