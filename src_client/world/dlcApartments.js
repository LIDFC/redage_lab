// Интерьеры квартир из DLC GTA5RP_APARTMENT (client_packages/dlcpacks/GTA5RP_APARTMENT/dlc.rpf).
// Карты интерьеров в DLC могут быть не активны по умолчанию — подгружаем их как IPL.
// Сервер: dotnet/resources/NeptuneEvo/Houses/Apartments/ApartmentInteriors.cs
const APARTMENT_IPLS = [
    "int_ap_house_1_1_milo_", "int_ap_house_1_2_milo_", "int_ap_house_1_3_milo_", "int_ap_house_1_4_milo_", "int_ap_house_1_5_milo_",
    "int_garage_milo_",
];

const loadApartmentIpls = () => {
    APARTMENT_IPLS.forEach((ipl) => {
        try {
            if (!mp.game.streaming.isIplActive(ipl))
                mp.game.streaming.requestIpl(ipl);
        } catch (e) {}
    });
};

loadApartmentIpls();
mp.events.add('playerSpawn', loadApartmentIpls);
